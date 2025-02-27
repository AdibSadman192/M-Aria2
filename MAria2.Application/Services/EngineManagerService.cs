using System.Collections.Concurrent;
using MAria2.Core.Interfaces;
using MAria2.Core.Enums;
using MAria2.Core.Entities;
using MAria2.Core.Services;

namespace MAria2.Application.Services;

public class EngineManagerService
{
    private readonly IEnumerable<IDownloadEngine> _downloadEngines;
    private readonly IEnumerable<IEngineCapabilityProvider> _capabilityProviders;
    private readonly ILoggingService _loggingService;
    private readonly ConcurrentDictionary<EngineType, EnginePerformanceMetrics> _enginePerformance = new();

    public EngineManagerService(
        IEnumerable<IDownloadEngine> downloadEngines,
        IEnumerable<IEngineCapabilityProvider> capabilityProviders,
        ILoggingService loggingService)
    {
        _downloadEngines = downloadEngines;
        _capabilityProviders = capabilityProviders;
        _loggingService = loggingService;

        // Initialize performance tracking
        foreach (var engine in downloadEngines)
        {
            _enginePerformance[engine.Type] = new EnginePerformanceMetrics();
        }
    }

    public IReadOnlyCollection<EngineType> GetSupportedEngines() =>
        _downloadEngines.Select(e => e.Type).ToList();

    public async Task<Download> SwitchDownloadEngineAsync(
        Download download, 
        EngineType targetEngine)
    {
        try 
        {
            // Validate engine switch
            if (!CanSwitchEngine(download, targetEngine))
            {
                throw new InvalidOperationException(
                    $"Cannot switch to engine {targetEngine} for download {download.Id}"
                );
            }

            // Get current engine
            var currentEngine = _downloadEngines
                .First(e => e.Type == download.SelectedEngine);

            // Pause current download
            await currentEngine.PauseDownloadAsync(download);

            // Log engine switch
            _loggingService.LogInformation(
                $"Switching download {download.Id} from {currentEngine.Type} to {targetEngine}"
            );

            // Get target engine
            var targetEngineImpl = _downloadEngines
                .First(e => e.Type == targetEngine);

            // Start download with new engine
            download.SelectedEngine = targetEngine;
            var result = await targetEngineImpl.StartDownloadAsync(download);

            // Update performance metrics
            UpdateEnginePerformance(currentEngine.Type, download, false);
            UpdateEnginePerformance(targetEngine, download, true);

            return result;
        }
        catch (Exception ex)
        {
            _loggingService.LogError(
                $"Engine switch failed: {ex.Message}", 
                ex
            );
            throw;
        }
    }

    public EngineType RecommendBestEngine(string url)
    {
        // Advanced engine recommendation
        var candidateEngines = _capabilityProviders
            .Where(p => p.SupportsProtocol(new Uri(url).Scheme))
            .ToList();

        if (!candidateEngines.Any())
        {
            throw new NotSupportedException(
                $"No engines support protocol for URL: {url}"
            );
        }

        // Combine capability priority with performance metrics
        return candidateEngines
            .Select(p => new 
            { 
                Provider = p, 
                Priority = p.GetPriority(url),
                Performance = _enginePerformance[p.EngineType].Score
            })
            .OrderByDescending(x => x.Priority * 0.6 + x.Performance * 0.4)
            .Select(x => x.Provider.EngineType)
            .First();
    }

    public bool CanSwitchEngine(Download download, EngineType targetEngine)
    {
        // Enhanced engine switch validation
        var currentCapabilityProvider = _capabilityProviders
            .FirstOrDefault(p => p.EngineType == download.SelectedEngine);

        var targetCapabilityProvider = _capabilityProviders
            .FirstOrDefault(p => p.EngineType == targetEngine);

        return targetCapabilityProvider != null && 
               targetCapabilityProvider.SupportsProtocol(new Uri(download.Url).Scheme) &&
               (currentCapabilityProvider?.CanPartiallyResume(download) ?? false);
    }

    private void UpdateEnginePerformance(
        EngineType engineType, 
        Download download, 
        bool isSuccessful)
    {
        var metrics = _enginePerformance[engineType];
        
        lock (metrics)
        {
            metrics.TotalAttempts++;
            metrics.SuccessfulAttempts += isSuccessful ? 1 : 0;
            
            // Update success rate
            metrics.SuccessRate = 
                (double)metrics.SuccessfulAttempts / metrics.TotalAttempts;

            // Calculate performance score (0-100)
            metrics.Score = Math.Round(metrics.SuccessRate * 100, 2);

            // Track download-specific metrics
            if (isSuccessful)
            {
                metrics.AverageDownloadSpeed = 
                    (metrics.AverageDownloadSpeed * (metrics.SuccessfulAttempts - 1) + 
                     download.AverageSpeed) / metrics.SuccessfulAttempts;
            }
        }
    }

    // Expose engine performance for monitoring
    public IReadOnlyDictionary<EngineType, EnginePerformanceMetrics> GetEnginePerformance() =>
        _enginePerformance.ToDictionary(
            kvp => kvp.Key, 
            kvp => kvp.Value
        );
}

// Performance tracking for download engines
public class EnginePerformanceMetrics
{
    public int TotalAttempts { get; set; }
    public int SuccessfulAttempts { get; set; }
    public double SuccessRate { get; set; }
    public double Score { get; set; }
    public double AverageDownloadSpeed { get; set; }
}
