using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface IPerformanceTrackingService
    {
        /// <summary>
        /// Tracks download performance metrics
        /// </summary>
        Task TrackDownloadPerformanceAsync(DownloadPerformanceMetric metric);

        /// <summary>
        /// Retrieves performance metrics for a specific time range
        /// </summary>
        Task<IEnumerable<DownloadPerformanceMetric>> GetPerformanceMetricsAsync(
            DateTime startTime, 
            DateTime endTime);

        /// <summary>
        /// Calculates overall download engine performance
        /// </summary>
        Task<EnginePerformanceSummary> CalculateEnginePerformanceAsync();

        /// <summary>
        /// Tracks system resource utilization
        /// </summary>
        Task TrackSystemResourceUtilizationAsync(SystemResourceMetric resourceMetric);

        /// <summary>
        /// Retrieves system resource usage history
        /// </summary>
        Task<IEnumerable<SystemResourceMetric>> GetSystemResourceHistoryAsync(
            DateTime startTime, 
            DateTime endTime);

        /// <summary>
        /// Generates performance optimization recommendations
        /// </summary>
        Task<IEnumerable<PerformanceOptimizationRecommendation>> GenerateOptimizationRecommendationsAsync();
    }

    public record DownloadPerformanceMetric
    {
        public DateTime Timestamp { get; init; }
        public string DownloadEngine { get; init; }
        public string Url { get; init; }
        public long FileSize { get; init; }
        public double DownloadSpeed { get; init; } // MB/s
        public TimeSpan DownloadDuration { get; init; }
        public bool WasSuccessful { get; init; }
    }

    public record SystemResourceMetric
    {
        public DateTime Timestamp { get; init; }
        public double CpuUsagePercent { get; init; }
        public long MemoryUsedBytes { get; init; }
        public long TotalMemoryBytes { get; init; }
        public double DiskReadSpeedMBps { get; init; }
        public double DiskWriteSpeedMBps { get; init; }
        public double NetworkUploadSpeedMbps { get; init; }
        public double NetworkDownloadSpeedMbps { get; init; }
    }

    public record EnginePerformanceSummary
    {
        public Dictionary<string, EnginePerformanceDetails> EnginePerformance { get; init; }
        public string BestPerformingEngine { get; init; }
    }

    public record EnginePerformanceDetails
    {
        public double AverageDownloadSpeed { get; init; }
        public double SuccessRate { get; init; }
        public int TotalDownloads { get; init; }
        public TimeSpan AverageDownloadTime { get; init; }
    }

    public record PerformanceOptimizationRecommendation
    {
        public string RecommendationType { get; init; }
        public string Description { get; init; }
        public int PriorityLevel { get; init; }
        public Dictionary<string, object> AdditionalDetails { get; init; }
    }
}
