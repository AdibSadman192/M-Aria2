using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using MAria2.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAria2.Application.Services
{
    public class MonitoringDashboardService : IMonitoringDashboardService
    {
        private readonly IDownloadEngineManager _engineManager;
        private readonly ISystemResourceMonitor _resourceMonitor;
        private readonly IErrorTrackingService _errorTracker;
        private readonly ILogger<MonitoringDashboardService> _logger;

        private static readonly ConcurrentQueue<DownloadPerformanceRecord> _performanceHistory 
            = new ConcurrentQueue<DownloadPerformanceRecord>();

        public MonitoringDashboardService(
            IDownloadEngineManager engineManager,
            ISystemResourceMonitor resourceMonitor,
            IErrorTrackingService errorTracker,
            ILogger<MonitoringDashboardService> logger)
        {
            _engineManager = engineManager;
            _resourceMonitor = resourceMonitor;
            _errorTracker = errorTracker;
            _logger = logger;
        }

        public async Task<DashboardMetrics> GetCurrentMetricsAsync()
        {
            try 
            {
                var activeDownloads = await _engineManager.GetActiveDownloadsAsync();
                return new DashboardMetrics
                {
                    TotalDownloads = activeDownloads.Count(),
                    AverageDownloadSpeed = activeDownloads.Average(d => d.DownloadSpeed),
                    ConcurrentDownloads = activeDownloads.Count()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current metrics");
                return new DashboardMetrics();
            }
        }

        public async Task<IEnumerable<DownloadPerformanceRecord>> GetDownloadPerformanceHistoryAsync(TimeSpan timeRange)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeRange);
            return _performanceHistory
                .Where(record => record.Timestamp >= cutoffTime)
                .OrderBy(record => record.Timestamp);
        }

        public async Task<IEnumerable<ErrorRecord>> GetRecentErrorsAsync(int count)
        {
            return await _errorTracker.GetRecentErrorsAsync(count);
        }

        public async Task<SystemResourceUsage> GetSystemResourceUsageAsync()
        {
            return await _resourceMonitor.GetCurrentResourceUsageAsync();
        }

        public async Task<EnginePerformanceSummary> GetEnginePerformanceSummaryAsync()
        {
            var engineStats = await _engineManager.GetEnginePerformanceStatsAsync();
            return new EnginePerformanceSummary
            {
                SuccessRates = engineStats.ToDictionary(
                    stat => stat.EngineName, 
                    stat => stat.SuccessRate
                ),
                EngineSwitchCount = await _engineManager.GetEngineSwitchCountAsync()
            };
        }

        public async Task<bool> CheckAlertConditionsAsync()
        {
            var metrics = await GetCurrentMetricsAsync();
            var resourceUsage = await GetSystemResourceUsageAsync();
            var errorCount = (await GetRecentErrorsAsync(50)).Count();

            bool hasAlerts = 
                metrics.AverageDownloadSpeed < 1 ||
                resourceUsage.CpuUsagePercentage > 90 ||
                resourceUsage.MemoryUsagePercentage > 90 ||
                errorCount > 25;

            if (hasAlerts)
            {
                _logger.LogWarning("Alert conditions detected in monitoring dashboard");
            }

            return hasAlerts;
        }

        // Background method to track performance history
        public void TrackDownloadPerformance(DownloadPerformanceRecord record)
        {
            _performanceHistory.Enqueue(record);

            // Limit history to last 1000 records to prevent memory growth
            while (_performanceHistory.Count > 1000)
            {
                _performanceHistory.TryDequeue(out _);
            }
        }
    }
}
