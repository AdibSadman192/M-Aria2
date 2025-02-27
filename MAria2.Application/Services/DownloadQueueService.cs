using System.Collections.Concurrent;
using System.Diagnostics;
using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;
using MAria2.Application.Models;

namespace MAria2.Application.Services;

public class DownloadQueueService : IDisposable
{
    // Advanced queue management
    private readonly PriorityBlockingCollection<Download> _downloadQueue;
    private readonly ConcurrentDictionary<Guid, DownloadState> _downloadStates = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    // Service dependencies
    private readonly DownloadService _downloadService;
    private readonly EngineSelectionService _engineSelectionService;
    private readonly ILoggingService _loggingService;
    private readonly IConfigurationService _configurationService;
    private readonly ProtocolHandlerService _protocolHandler;

    // Queue configuration
    private readonly int _maxConcurrentDownloads;
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _retryBackoffBase = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _downloadSemaphore;

    // Advanced events with more context
    public event EventHandler<DownloadQueueEvent> QueueEvent;

    public DownloadQueueService(
        DownloadService downloadService,
        EngineSelectionService engineSelectionService,
        ProtocolHandlerService protocolHandler,
        ILoggingService loggingService,
        IConfigurationService configurationService,
        int maxConcurrentDownloads = 5,
        int maxRetryAttempts = 3)
    {
        _downloadService = downloadService;
        _engineSelectionService = engineSelectionService;
        _protocolHandler = protocolHandler;
        _loggingService = loggingService;
        _configurationService = configurationService;

        _maxConcurrentDownloads = maxConcurrentDownloads;
        _maxRetryAttempts = maxRetryAttempts;
        _downloadSemaphore = new SemaphoreSlim(maxConcurrentDownloads);
        
        // Initialize priority-based queue
        _downloadQueue = new PriorityBlockingCollection<Download>(
            download => GetDownloadPriorityScore(download)
        );

        // Start background queue processing
        _ = ProcessQueueAsync(_cancellationTokenSource.Token);
    }

    // Advanced priority scoring
    private int GetDownloadPriorityScore(Download download)
    {
        int baseScore = download.Priority switch
        {
            DownloadPriority.High => 100,
            DownloadPriority.Medium => 50,
            DownloadPriority.Low => 10,
            _ => 25
        };

        // Consider download size and time in queue
        var timeInQueue = DateTime.UtcNow - download.QueuedAt;
        baseScore += (int)Math.Min(timeInQueue.TotalMinutes, 60);

        // Penalize failed downloads
        var downloadState = _downloadStates.GetValueOrDefault(download.Id);
        if (downloadState?.FailureCount > 0)
        {
            baseScore -= downloadState.FailureCount * 10;
        }

        return baseScore;
    }

    public async Task EnqueueDownloadAsync(Download download)
    {
        // Advanced engine selection with protocol analysis
        try 
        {
            var protocolRequest = await _protocolHandler.AnalyzeDownloadRequestAsync(download.Url);
            var bestEngine = await _engineSelectionService.SelectBestEngineAsync(protocolRequest);
            
            download.PreferredEngine = bestEngine.GetType().Name;
            download.Status = DownloadStatus.Queued;
            download.QueuedAt = DateTime.UtcNow;

            // Track download state
            _downloadStates[download.Id] = new DownloadState();
            
            _downloadQueue.Add(download);

            // Raise detailed queue event
            QueueEvent?.Invoke(this, new DownloadQueueEvent(
                EventType.Enqueued, 
                download, 
                $"Download queued: {download.Url}"
            ));
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Enqueue failed: {ex.Message}");
            
            QueueEvent?.Invoke(this, new DownloadQueueEvent(
                EventType.EnqueueFailed, 
                download, 
                $"Failed to enqueue download: {ex.Message}"
            ));
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try 
            {
                await _downloadSemaphore.WaitAsync(cancellationToken);

                if (_downloadQueue.TryTake(out var download))
                {
                    _ = ProcessDownloadAsync(download);
                }
                else 
                {
                    // Intelligent idle management
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Queue processing error: {ex.Message}");
            }
            finally 
            {
                _downloadSemaphore.Release();
            }
        }
    }

    private async Task ProcessDownloadAsync(Download download)
    {
        var downloadState = _downloadStates[download.Id];
        var stopwatch = Stopwatch.StartNew();

        try 
        {
            download.Status = DownloadStatus.Downloading;
            QueueEvent?.Invoke(this, new DownloadQueueEvent(
                EventType.DownloadStarted, 
                download
            ));

            await _downloadService.StartDownloadAsync(
                download.Url, 
                download.DestinationPath, 
                download.PreferredEngine
            );

            // Track performance metrics
            stopwatch.Stop();
            _protocolHandler.TrackEnginePerformance(
                _downloadService.GetCurrentEngine(download), 
                new ProtocolHandlerService.DownloadMetrics
                {
                    IsSuccessful = true,
                    DownloadSpeed = download.AverageSpeed,
                    DownloadTime = stopwatch.Elapsed.TotalSeconds,
                    FileSize = download.FileSize
                }
            );

            download.Status = DownloadStatus.Completed;
            QueueEvent?.Invoke(this, new DownloadQueueEvent(
                EventType.DownloadCompleted, 
                download
            ));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            downloadState.FailureCount++;

            // Intelligent retry mechanism
            if (downloadState.FailureCount <= _maxRetryAttempts)
            {
                var backoffTime = _retryBackoffBase * Math.Pow(2, downloadState.FailureCount);
                
                _loggingService.LogWarning(
                    $"Download failed. Retry {downloadState.FailureCount}. " +
                    $"Backing off for {backoffTime.TotalSeconds} seconds."
                );

                await Task.Delay(backoffTime);

                // Attempt engine switching
                try 
                {
                    var currentEngine = _downloadService.GetCurrentEngine(download);
                    var newEngine = await _engineSelectionService.SwitchEngineForDownloadAsync(
                        currentEngine, 
                        download
                    );

                    download.PreferredEngine = newEngine.GetType().Name;
                    
                    // Re-enqueue for retry
                    await EnqueueDownloadAsync(download);
                }
                catch 
                {
                    // Final failure
                    HandleDownloadFailure(download, ex);
                }
            }
            else 
            {
                // Final failure after max retries
                HandleDownloadFailure(download, ex);
            }
        }
        finally 
        {
            _downloadStates.TryRemove(download.Id, out _);
        }
    }

    private void HandleDownloadFailure(Download download, Exception ex)
    {
        download.Status = DownloadStatus.Failed;
        _loggingService.LogError($"Download ultimately failed: {ex.Message}");
        
        QueueEvent?.Invoke(this, new DownloadQueueEvent(
            EventType.DownloadFailed, 
            download, 
            ex.Message
        ));
    }

    // Advanced queue management methods
    public void PrioritizeDownload(Guid downloadId)
    {
        var download = _downloadQueue.FirstOrDefault(d => d.Id == downloadId);
        if (download != null)
        {
            download.Priority = DownloadPriority.High;
            _downloadQueue.Reorder();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _downloadSemaphore.Dispose();
        _cancellationTokenSource.Dispose();
        _downloadQueue.Dispose();
    }
}

// Advanced event model for queue events
public class DownloadQueueEvent : EventArgs
{
    public EventType Type { get; }
    public Download Download { get; }
    public string Message { get; }

    public DownloadQueueEvent(
        EventType type, 
        Download download, 
        string message = null)
    {
        Type = type;
        Download = download;
        Message = message;
    }
}

public enum EventType
{
    Enqueued,
    EnqueueFailed,
    DownloadStarted,
    DownloadCompleted,
    DownloadFailed
}

// Tracks additional state for each download
internal class DownloadState
{
    public int FailureCount { get; set; }
    public DateTime LastFailureTime { get; set; }
}

// Custom priority-based blocking collection
internal class PriorityBlockingCollection<T> : IDisposable
{
    private readonly BlockingCollection<T> _collection = new();
    private readonly Func<T, int> _prioritySelector;

    public PriorityBlockingCollection(Func<T, int> prioritySelector)
    {
        _prioritySelector = prioritySelector;
    }

    public void Add(T item)
    {
        _collection.Add(item);
        Reorder();
    }

    public bool TryTake(out T item)
    {
        item = default;
        
        if (_collection.Count == 0)
            return false;

        // Find highest priority item
        var prioritizedItems = _collection
            .OrderByDescending(_prioritySelector)
            .ToList();

        item = prioritizedItems.First();
        _collection.TryTake(out _);

        return true;
    }

    public void Reorder()
    {
        // Reorder items based on priority
        var items = _collection.ToList();
        _collection.Dispose();
        
        foreach (var item in items.OrderByDescending(_prioritySelector))
        {
            _collection.Add(item);
        }
    }

    public void Dispose()
    {
        _collection.Dispose();
    }

    public int Count => _collection.Count;
    public IEnumerable<T> ToArray() => _collection.ToArray();
    public T FirstOrDefault(Func<T, bool> predicate) => 
        _collection.FirstOrDefault(predicate);
}
