using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface IDownloadEngineManager
    {
        /// <summary>
        /// Register a new download engine
        /// </summary>
        void RegisterEngine(IDownloadEngine engine);

        /// <summary>
        /// Unregister a download engine
        /// </summary>
        void UnregisterEngine(string engineName);

        /// <summary>
        /// Select the most appropriate download engine for a given URL
        /// </summary>
        Task<IDownloadEngine> SelectBestEngineAsync(string url);

        /// <summary>
        /// Download a file using the most suitable engine
        /// </summary>
        Task<DownloadResult> DownloadFileAsync(
            string url, 
            string destinationPath, 
            DownloadPriority priority = DownloadPriority.Normal,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get performance metrics for all registered engines
        /// </summary>
        Task<IEnumerable<DownloadEnginePerformanceMetrics>> GetEnginePerformanceMetricsAsync();

        /// <summary>
        /// Get all registered download engines
        /// </summary>
        IEnumerable<IDownloadEngine> GetRegisteredEngines();

        /// <summary>
        /// Set download engine selection strategy
        /// </summary>
        void SetEngineSelectionStrategy(IEngineSelectionStrategy strategy);
    }

    public interface IEngineSelectionStrategy
    {
        /// <summary>
        /// Determine the most suitable download engine for a given URL
        /// </summary>
        Task<IDownloadEngine> SelectEngineAsync(
            IEnumerable<IDownloadEngine> availableEngines, 
            string url);
    }

    public record DownloadEnginePerformanceMetrics
    {
        public string EngineName { get; init; }
        public int TotalDownloads { get; init; }
        public double AverageDownloadSpeed { get; init; }
        public double SuccessRate { get; init; }
        public TimeSpan AverageDownloadTime { get; init; }
        public long TotalBytesDownloaded { get; init; }
    }

    public enum DownloadPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public class DefaultEngineSelectionStrategy : IEngineSelectionStrategy
    {
        public async Task<IDownloadEngine> SelectEngineAsync(
            IEnumerable<IDownloadEngine> availableEngines, 
            string url)
        {
            // Basic implementation: first available engine
            foreach (var engine in availableEngines)
            {
                // Add more sophisticated selection logic here
                return engine;
            }

            throw new InvalidOperationException("No suitable download engine found");
        }
    }
}
