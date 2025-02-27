using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MAria2.Presentation.Interfaces
{
    public interface IMonitoringDashboardService
    {
        Task<DashboardMetrics> GetCurrentMetricsAsync();
        Task<IEnumerable<DownloadPerformanceRecord>> GetDownloadPerformanceHistoryAsync(TimeSpan timeRange);
        Task<IEnumerable<ErrorRecord>> GetRecentErrorsAsync(int count);
        Task<SystemResourceUsage> GetSystemResourceUsageAsync();
        Task<EnginePerformanceSummary> GetEnginePerformanceSummaryAsync();
        Task<bool> CheckAlertConditionsAsync();
    }

    public record DashboardMetrics
    {
        public int TotalDownloads { get; init; }
        public double AverageDownloadSpeed { get; init; }
        public int ConcurrentDownloads { get; init; }
    }

    public record DownloadPerformanceRecord
    {
        public DateTime Timestamp { get; init; }
        public double DownloadSpeed { get; init; }
        public string DownloadEngine { get; init; }
    }

    public record ErrorRecord
    {
        public DateTime Timestamp { get; init; }
        public string ErrorCategory { get; init; }
        public string ErrorMessage { get; init; }
    }

    public record SystemResourceUsage
    {
        public double CpuUsagePercentage { get; init; }
        public double MemoryUsagePercentage { get; init; }
        public double DiskIoSpeed { get; init; }
    }

    public record EnginePerformanceSummary
    {
        public Dictionary<string, double> SuccessRates { get; init; }
        public int EngineSwitchCount { get; init; }
    }
}
