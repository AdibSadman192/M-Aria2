using System.Collections.Concurrent;
using MAria2.Core.Entities;
using MAria2.Core.Interfaces;
using MAria2.Core.Enums;

namespace MAria2.Application.Services;

public class EngineHealthMonitorService : IDisposable
{
    private readonly IEnumerable<IDownloadEngine> _downloadEngines;
    private readonly ILoggingService _loggingService;

    // Thread-safe collections for tracking engine performance
    private readonly ConcurrentDictionary<string, EngineHealthMetrics> _engineHealthMetrics = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DownloadPerformanceRecord>> _performanceHistory = new();

    // Periodic health check configuration
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(5);
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // Events for health status changes
    public event EventHandler<EngineHealthChangedEventArgs> EngineHealthChanged;

    public EngineHealthMonitorService(
        IEnumerable<IDownloadEngine> downloadEngines,
        ILoggingService loggingService)
    {
        _downloadEngines = downloadEngines;
        _loggingService = loggingService;

        // Initialize health metrics for each engine
        foreach (var engine in _downloadEngines)
        {
            var engineName = engine.GetType().Name;
            _engineHealthMetrics[engineName] = new EngineHealthMetrics();
            _performanceHistory[engineName] = new ConcurrentQueue<DownloadPerformanceRecord>();
        }

        // Start background health monitoring
        _ = StartPeriodicHealthMonitoringAsync(_cancellationTokenSource.Token);
    }

    private async Task StartPeriodicHealthMonitoringAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try 
            {
                await PerformHealthCheckAsync();
                await Task.Delay(_healthCheckInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Health monitoring error: {ex.Message}");
            }
        }
    }

    private async Task PerformHealthCheckAsync()
    {
        var healthCheckTasks = _downloadEngines.Select(async engine =>
        {
            var engineName = engine.GetType().Name;
            try 
            {
                var healthResult = await PerformSingleEngineHealthCheckAsync(engine);
                UpdateEngineHealthMetrics(engineName, healthResult);
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning(
                    $"Health check failed for {engineName}: {ex.Message}"
                );
            }
        });

        await Task.WhenAll(healthCheckTasks);
    }

    private async Task<EngineHealthCheckResult> PerformSingleEngineHealthCheckAsync(IDownloadEngine engine)
    {
        var testUrl = GetTestUrlForEngine(engine);
        
        var startTime = DateTime.UtcNow;
        var result = new EngineHealthCheckResult { StartTime = startTime };

        try 
        {
            var performanceTest = await engine.TestPerformanceAsync(testUrl);
            
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.SpeedMbps = performanceTest.SpeedMbps;
            result.ConnectionStability = performanceTest.ConnectionStability;
            result.Status = EngineHealthStatus.Healthy;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.Status = EngineHealthStatus.Degraded;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private void UpdateEngineHealthMetrics(
        string engineName, 
        EngineHealthCheckResult healthResult)
    {
        var metrics = _engineHealthMetrics[engineName];
        var performanceHistory = _performanceHistory[engineName];

        // Update performance record
        var performanceRecord = new DownloadPerformanceRecord
        {
            Timestamp = healthResult.EndTime,
            Speed = healthResult.SpeedMbps,
            Status = healthResult.Status
        };
        performanceHistory.Enqueue(performanceRecord);

        // Maintain a limited history
        while (performanceHistory.Count > 100)
        {
            performanceHistory.TryDequeue(out _);
        }

        // Update health metrics
        metrics.LastHealthCheckTime = healthResult.EndTime;
        metrics.AverageSpeed = performanceHistory
            .Where(r => r.Status == EngineHealthStatus.Healthy)
            .Select(r => r.Speed)
            .DefaultIfEmpty(0)
            .Average();
        
        metrics.HealthStatus = healthResult.Status;
        
        // Calculate health score
        metrics.HealthScore = CalculateHealthScore(performanceHistory);

        // Trigger health change event if significant
        if (metrics.HealthScore < 0.5)
        {
            EngineHealthChanged?.Invoke(
                this, 
                new EngineHealthChangedEventArgs(
                    engineName, 
                    metrics.HealthStatus, 
                    metrics.HealthScore
                )
            );
        }
    }

    private double CalculateHealthScore(
        ConcurrentQueue<DownloadPerformanceRecord> performanceHistory)
    {
        // Complex health score calculation
        var healthyRecords = performanceHistory
            .Where(r => r.Status == EngineHealthStatus.Healthy)
            .ToList();

        if (!healthyRecords.Any())
            return 0;

        // Weighted calculation considering recent performance
        double recentPerformanceWeight = 0.6;
        double historicalPerformanceWeight = 0.4;

        var recentScore = healthyRecords
            .TakeLast(10)
            .Select(r => r.Speed)
            .DefaultIfEmpty(0)
            .Average() / 100.0;  // Normalize

        var historicalScore = healthyRecords
            .Select(r => r.Speed)
            .DefaultIfEmpty(0)
            .Average() / 100.0;  // Normalize

        return (recentPerformanceWeight * recentScore) + 
               (historicalPerformanceWeight * historicalScore);
    }

    private string GetTestUrlForEngine(IDownloadEngine engine)
    {
        // Predefined test URLs for different engine types
        return engine switch
        {
            var e when e.GetType().Name.Contains("Aria2") => 
                "https://speed.hetzner.de/100MB.bin",
            var e when e.GetType().Name.Contains("YtDlp") => 
                "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            var e when e.GetType().Name.Contains("WinInet") => 
                "https://download.microsoft.com/download/2/0/E/20E90413-712F-438C-988E-FDAA79A8AC3D/dotnetfx35.exe",
            _ => "https://speed.cloudflare.com/__down?bytes=104857600"
        };
    }

    public EngineHealthMetrics GetEngineHealthMetrics(string engineName)
    {
        return _engineHealthMetrics.TryGetValue(engineName, out var metrics)
            ? metrics
            : null;
    }

    public IReadOnlyCollection<DownloadPerformanceRecord> GetEnginePerformanceHistory(string engineName)
    {
        return _performanceHistory.TryGetValue(engineName, out var history)
            ? history.ToList()
            : new List<DownloadPerformanceRecord>();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    // Nested classes and records for health tracking
    public class EngineHealthMetrics
    {
        public DateTime LastHealthCheckTime { get; set; }
        public EngineHealthStatus HealthStatus { get; set; }
        public double HealthScore { get; set; }
        public double AverageSpeed { get; set; }
    }

    public record DownloadPerformanceRecord
    {
        public DateTime Timestamp { get; init; }
        public double Speed { get; init; }
        public EngineHealthStatus Status { get; init; }
    }

    public class EngineHealthCheckResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public double SpeedMbps { get; set; }
        public double ConnectionStability { get; set; }
        public EngineHealthStatus Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class EngineHealthChangedEventArgs : EventArgs
    {
        public string EngineName { get; }
        public EngineHealthStatus HealthStatus { get; }
        public double HealthScore { get; }

        public EngineHealthChangedEventArgs(
            string engineName, 
            EngineHealthStatus healthStatus, 
            double healthScore)
        {
            EngineName = engineName;
            HealthStatus = healthStatus;
            HealthScore = healthScore;
        }
    }
}
