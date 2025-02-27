using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;

namespace MAria2.Core.Interfaces
{
    public interface IResourceDashboardService
    {
        /// <summary>
        /// Get real-time dashboard data
        /// </summary>
        Task<ResourceDashboardData> GetCurrentDashboardDataAsync();

        /// <summary>
        /// Get historical resource performance data
        /// </summary>
        Task<IEnumerable<HistoricalResourceData>> GetHistoricalResourceDataAsync(
            TimeSpan timeRange, 
            ResourceDataGranularity granularity = ResourceDataGranularity.Minute);

        /// <summary>
        /// Subscribe to real-time resource updates
        /// </summary>
        IAsyncEnumerable<ResourceDashboardData> SubscribeToResourceUpdatesAsync(
            TimeSpan updateInterval);

        /// <summary>
        /// Get current resource alerts
        /// </summary>
        Task<IEnumerable<ResourceAlert>> GetCurrentAlertsAsync();

        /// <summary>
        /// Predict future resource utilization
        /// </summary>
        Task<ResourceUtilizationForecast> PredictResourceUtilizationAsync(
            TimeSpan forecastPeriod);
    }

    public record ResourceDashboardData
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        // System-Wide Metrics
        public SystemResourceMetrics SystemResources { get; init; }

        // Download-Specific Metrics
        public DownloadResourceMetrics DownloadResources { get; init; }

        // Performance Indicators
        public PerformanceIndicators PerformanceMetrics { get; init; }
    }

    public record SystemResourceMetrics
    {
        public double CpuUtilization { get; init; }
        public double MemoryUtilization { get; init; }
        public double NetworkUtilization { get; init; }
        public double DiskUtilization { get; init; }
        public double SystemTemperature { get; init; }
    }

    public record DownloadResourceMetrics
    {
        public int ActiveDownloads { get; init; }
        public long TotalBytesDownloaded { get; init; }
        public double AverageDownloadSpeed { get; init; }
        public int DownloadQueueLength { get; init; }
    }

    public record PerformanceIndicators
    {
        public double OverallSystemPerformanceScore { get; init; }
        public double DownloadPerformanceScore { get; init; }
        public bool IsSystemOverloaded { get; init; }
    }

    public record HistoricalResourceData
    {
        public DateTime Timestamp { get; init; }
        public double CpuUtilization { get; init; }
        public double MemoryUtilization { get; init; }
        public double NetworkUtilization { get; init; }
    }

    public record ResourceUtilizationForecast
    {
        public DateTime ForecastStart { get; init; }
        public TimeSpan ForecastPeriod { get; init; }
        public List<ForecastDataPoint> PredictedUtilization { get; init; }
    }

    public record ForecastDataPoint
    {
        public DateTime Timestamp { get; init; }
        public double PredictedCpuUtilization { get; init; }
        public double PredictedMemoryUtilization { get; init; }
    }

    public enum ResourceDataGranularity
    {
        Second,
        Minute,
        FiveMinutes,
        Hourly,
        Daily
    }
}
