using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using MAria2.Core.Models;

namespace MAria2.Application.Services
{
    public class AdvancedEngineSelectionStrategy : IEngineSelectionStrategy
    {
        private readonly IPerformanceTrackingService _performanceTracker;

        public AdvancedEngineSelectionStrategy(
            IPerformanceTrackingService performanceTracker)
        {
            _performanceTracker = performanceTracker;
        }

        public async Task<IDownloadEngine> SelectEngineAsync(
            IEnumerable<IDownloadEngine> availableEngines, 
            string url)
        {
            // Validate input
            if (availableEngines == null || !availableEngines.Any())
            {
                throw new ArgumentException("No download engines available");
            }

            // Retrieve performance metrics for all engines
            var enginePerformance = await GetEnginePerformanceAsync(availableEngines);

            // URL-specific engine selection
            var urlSpecificEngine = SelectUrlSpecificEngine(availableEngines, url);
            if (urlSpecificEngine != null)
            {
                return urlSpecificEngine;
            }

            // Performance-based selection
            var bestPerformingEngine = enginePerformance
                .OrderByDescending(p => p.SuccessRate)
                .ThenByDescending(p => p.AverageDownloadSpeed)
                .FirstOrDefault()?.Engine;

            if (bestPerformingEngine != null)
            {
                return bestPerformingEngine;
            }

            // Fallback to first available engine
            return availableEngines.First();
        }

        private async Task<IEnumerable<EnginePerformanceScore>> GetEnginePerformanceAsync(
            IEnumerable<IDownloadEngine> availableEngines)
        {
            var performanceScores = new List<EnginePerformanceScore>();

            foreach (var engine in availableEngines)
            {
                var metrics = await _performanceTracker.GetDownloadEngineMetricsAsync(
                    engine.EngineMetadata.EngineName
                );

                performanceScores.Add(new EnginePerformanceScore
                {
                    Engine = engine,
                    SuccessRate = metrics.SuccessRate,
                    AverageDownloadSpeed = metrics.AverageDownloadSpeed,
                    TotalDownloads = metrics.TotalDownloads
                });
            }

            return performanceScores;
        }

        private IDownloadEngine SelectUrlSpecificEngine(
            IEnumerable<IDownloadEngine> availableEngines, 
            string url)
        {
            // URL-specific engine selection logic
            var urlLower = url.ToLowerInvariant();

            // Example rules (expand as needed)
            if (urlLower.Contains("youtube.com"))
            {
                return availableEngines
                    .FirstOrDefault(e => e.EngineMetadata.EngineName.Contains("YoutubeDL"));
            }

            if (urlLower.Contains("github.com"))
            {
                return availableEngines
                    .FirstOrDefault(e => e.EngineMetadata.EngineName.Contains("Wget"));
            }

            return null;
        }

        private class EnginePerformanceScore
        {
            public IDownloadEngine Engine { get; set; }
            public double SuccessRate { get; set; }
            public double AverageDownloadSpeed { get; set; }
            public int TotalDownloads { get; set; }
        }
    }
}
