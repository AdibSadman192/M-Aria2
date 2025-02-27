using System.Collections.Concurrent;
using MAria2.Core.Entities;
using MAria2.Core.Interfaces;
using MAria2.Core.Enums;

namespace MAria2.Application.Services;

public class EngineSelectionService
{
    private readonly IEnumerable<IDownloadEngine> _availableEngines;
    private readonly ConcurrentDictionary<string, EnginePerformanceMetrics> _enginePerformance;
    private readonly IConfigurationService _configurationService;
    private readonly ILoggingService _loggingService;

    public EngineSelectionService(
        IEnumerable<IDownloadEngine> availableEngines,
        IConfigurationService configurationService,
        ILoggingService loggingService)
    {
        _availableEngines = availableEngines;
        _configurationService = configurationService;
        _loggingService = loggingService;
        _enginePerformance = new ConcurrentDictionary<string, EnginePerformanceMetrics>();
    }

    public async Task<IDownloadEngine> SelectBestEngineAsync(DownloadRequest request)
    {
        // 1. Get user-defined preferences
        var userPreferences = _configurationService.GetEnginePreferences();

        // 2. Filter compatible engines
        var compatibleEngines = _availableEngines
            .Where(engine => engine.CanHandleProtocol(request.Url))
            .ToList();

        if (!compatibleEngines.Any())
        {
            throw new InvalidOperationException($"No compatible engines found for URL: {request.Url}");
        }

        // 3. Apply user preferences first
        if (userPreferences.PreferredEngine != null)
        {
            var preferredEngine = compatibleEngines
                .FirstOrDefault(e => e.GetType().Name == userPreferences.PreferredEngine);
            
            if (preferredEngine != null)
            {
                return preferredEngine;
            }
        }

        // 4. Performance-based selection
        var engineScores = await EvaluateEnginePerformanceAsync(compatibleEngines, request);
        var bestEngine = engineScores.OrderByDescending(x => x.Score).First().Engine;

        _loggingService.LogInformation($"Selected engine {bestEngine.GetType().Name} for URL: {request.Url}");
        return bestEngine;
    }

    private async Task<List<(IDownloadEngine Engine, double Score)>> EvaluateEnginePerformanceAsync(
        List<IDownloadEngine> engines, 
        DownloadRequest request)
    {
        var performanceEvaluations = new List<Task<(IDownloadEngine, double)>>();

        foreach (var engine in engines)
        {
            performanceEvaluations.Add(Task.Run(async () => 
            {
                var metrics = await EvaluateEngineSingleMetricAsync(engine, request);
                return (engine, metrics);
            }));
        }

        return await Task.WhenAll(performanceEvaluations);
    }

    private async Task<double> EvaluateEngineSingleMetricAsync(IDownloadEngine engine, DownloadRequest request)
    {
        double score = 0;

        try 
        {
            // Performance metric calculation
            var startTime = DateTime.Now;
            var performanceTest = await engine.TestPerformanceAsync(request.Url);
            var duration = (DateTime.Now - startTime).TotalMilliseconds;

            score += performanceTest.SpeedMbps * 0.4;  // Speed weight
            score += (1 / duration) * 0.3;  // Responsiveness weight
            score += performanceTest.ConnectionStability * 0.3;  // Stability weight

            // Update performance metrics
            _enginePerformance.AddOrUpdate(
                engine.GetType().Name, 
                new EnginePerformanceMetrics { LastScore = score },
                (key, oldValue) => 
                {
                    oldValue.LastScore = score;
                    oldValue.AverageScore = (oldValue.AverageScore + score) / 2;
                    return oldValue;
                }
            );
        }
        catch (Exception ex)
        {
            _loggingService.LogWarning($"Performance test failed for {engine.GetType().Name}: {ex.Message}");
            score = 0.1;  // Minimal score for failed tests
        }

        return score;
    }

    public class EnginePerformanceMetrics
    {
        public double LastScore { get; set; }
        public double AverageScore { get; set; }
        public int TestCount { get; set; }
        public DateTime LastTestedAt { get; set; }
    }

    public async Task<IDownloadEngine> SwitchEngineForDownloadAsync(
        IDownloadEngine currentEngine, 
        Download download)
    {
        // Fallback engine switching logic
        var compatibleEngines = _availableEngines
            .Where(e => e.CanHandleProtocol(download.Url) && e != currentEngine)
            .ToList();

        if (!compatibleEngines.Any())
        {
            throw new InvalidOperationException("No alternative engines available");
        }

        var newEngine = await SelectBestEngineAsync(new DownloadRequest(download.Url));
        
        // Attempt to transfer download state
        await newEngine.ResumeDownloadAsync(download);

        _loggingService.LogInformation(
            $"Switched download engine from {currentEngine.GetType().Name} " +
            $"to {newEngine.GetType().Name} for URL: {download.Url}"
        );

        return newEngine;
    }
}
