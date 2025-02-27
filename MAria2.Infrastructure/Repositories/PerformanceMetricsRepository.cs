using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Repositories
{
    public class PerformanceMetricsRepository
    {
        private readonly string _metricsStoragePath;
        private readonly ILogger<PerformanceMetricsRepository> _logger;
        private const string DOWNLOAD_METRICS_FILENAME = "download_metrics.json";
        private const string RESOURCE_METRICS_FILENAME = "system_resource_metrics.json";

        public PerformanceMetricsRepository(
            IPlatformAbstractionService platformService,
            ILogger<PerformanceMetricsRepository> logger)
        {
            _logger = logger;
            var downloadDir = platformService.GetDefaultDownloadDirectory();
            
            // Create dedicated performance metrics directory
            _metricsStoragePath = Path.Combine(
                downloadDir, 
                ".maria2", 
                "performance_metrics"
            );
            
            Directory.CreateDirectory(_metricsStoragePath);
        }

        public async Task SaveDownloadMetricsAsync(IEnumerable<DownloadPerformanceMetric> metrics)
        {
            try 
            {
                var filePath = Path.Combine(_metricsStoragePath, DOWNLOAD_METRICS_FILENAME);
                var jsonContent = JsonSerializer.Serialize(metrics, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                await File.WriteAllTextAsync(filePath, jsonContent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving download metrics: {ex.Message}");
            }
        }

        public async Task<IEnumerable<DownloadPerformanceMetric>> LoadDownloadMetricsAsync(
            DateTime? startTime = null, 
            DateTime? endTime = null)
        {
            try 
            {
                var filePath = Path.Combine(_metricsStoragePath, DOWNLOAD_METRICS_FILENAME);
                
                if (!File.Exists(filePath))
                    return Enumerable.Empty<DownloadPerformanceMetric>();

                var jsonContent = await File.ReadAllTextAsync(filePath);
                var allMetrics = JsonSerializer.Deserialize<List<DownloadPerformanceMetric>>(jsonContent) 
                    ?? Enumerable.Empty<DownloadPerformanceMetric>();

                // Apply time filtering if provided
                return allMetrics.Where(m => 
                    (!startTime.HasValue || m.Timestamp >= startTime.Value) &&
                    (!endTime.HasValue || m.Timestamp <= endTime.Value)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading download metrics: {ex.Message}");
                return Enumerable.Empty<DownloadPerformanceMetric>();
            }
        }

        public async Task SaveSystemResourceMetricsAsync(IEnumerable<SystemResourceMetric> metrics)
        {
            try 
            {
                var filePath = Path.Combine(_metricsStoragePath, RESOURCE_METRICS_FILENAME);
                var jsonContent = JsonSerializer.Serialize(metrics, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                await File.WriteAllTextAsync(filePath, jsonContent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving system resource metrics: {ex.Message}");
            }
        }

        public async Task<IEnumerable<SystemResourceMetric>> LoadSystemResourceMetricsAsync(
            DateTime? startTime = null, 
            DateTime? endTime = null)
        {
            try 
            {
                var filePath = Path.Combine(_metricsStoragePath, RESOURCE_METRICS_FILENAME);
                
                if (!File.Exists(filePath))
                    return Enumerable.Empty<SystemResourceMetric>();

                var jsonContent = await File.ReadAllTextAsync(filePath);
                var allMetrics = JsonSerializer.Deserialize<List<SystemResourceMetric>>(jsonContent) 
                    ?? Enumerable.Empty<SystemResourceMetric>();

                // Apply time filtering if provided
                return allMetrics.Where(m => 
                    (!startTime.HasValue || m.Timestamp >= startTime.Value) &&
                    (!endTime.HasValue || m.Timestamp <= endTime.Value)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading system resource metrics: {ex.Message}");
                return Enumerable.Empty<SystemResourceMetric>();
            }
        }

        public async Task PruneOldMetricsAsync(TimeSpan retentionPeriod)
        {
            try 
            {
                var cutoffTime = DateTime.UtcNow.Subtract(retentionPeriod);

                // Prune download metrics
                var downloadMetrics = await LoadDownloadMetricsAsync();
                var prunedDownloadMetrics = downloadMetrics
                    .Where(m => m.Timestamp >= cutoffTime)
                    .ToList();
                await SaveDownloadMetricsAsync(prunedDownloadMetrics);

                // Prune system resource metrics
                var resourceMetrics = await LoadSystemResourceMetricsAsync();
                var prunedResourceMetrics = resourceMetrics
                    .Where(m => m.Timestamp >= cutoffTime)
                    .ToList();
                await SaveSystemResourceMetricsAsync(prunedResourceMetrics);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error pruning old metrics: {ex.Message}");
            }
        }
    }
}
