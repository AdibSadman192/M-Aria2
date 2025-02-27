using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Services
{
    public class ResourceTrackingService : IResourceTrackingService
    {
        private readonly ILogger<ResourceTrackingService> _logger;
        private readonly ConcurrentDictionary<string, ResourceMetrics> _resourceHistory;
        private ResourceThresholds _resourceThresholds;

        public ResourceTrackingService(ILogger<ResourceTrackingService> logger)
        {
            _logger = logger;
            _resourceHistory = new ConcurrentDictionary<string, ResourceMetrics>();
            _resourceThresholds = new ResourceThresholds();
        }

        public async Task<ResourceTrackingContext> StartTrackingAsync(string operationId)
        {
            return new ResourceTrackingContext
            {
                OperationId = operationId
            };
        }

        public async Task<ResourceMetrics> StopTrackingAsync(ResourceTrackingContext context)
        {
            var metrics = await GetCurrentResourceUtilizationAsync();
            metrics = metrics with { OperationId = context.OperationId };

            await LogResourceHistoryAsync(metrics);
            CheckResourceThresholds(metrics);

            return metrics;
        }

        public async Task<ResourceMetrics> GetCurrentResourceUtilizationAsync()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? await GetWindowsResourceMetricsAsync() 
                : await GetUnixResourceMetricsAsync();
        }

        public async Task LogResourceHistoryAsync(ResourceMetrics metrics)
        {
            _resourceHistory[metrics.OperationId] = metrics;

            // Limit history size
            if (_resourceHistory.Count > 1000)
            {
                var oldestKey = _resourceHistory.Keys
                    .OrderBy(k => _resourceHistory[k].Timestamp)
                    .First();
                _resourceHistory.TryRemove(oldestKey, out _);
            }
        }

        public async Task<IEnumerable<ResourceMetrics>> GetResourceHistoryAsync(
            TimeSpan? timeRange = null, 
            ResourceType? resourceType = null)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeRange ?? TimeSpan.FromHours(1));

            return _resourceHistory.Values
                .Where(m => m.Timestamp >= cutoffTime)
                .Where(m => resourceType == null || 
                    (resourceType == ResourceType.Cpu && m.CpuUtilization > 0) ||
                    (resourceType == ResourceType.Memory && m.MemoryUtilizationPercentage > 0) ||
                    (resourceType == ResourceType.Network && m.NetworkUtilizationPercentage > 0) ||
                    (resourceType == ResourceType.Disk && m.DiskUtilizationPercentage > 0))
                .OrderByDescending(m => m.Timestamp);
        }

        public void SetResourceThresholds(ResourceThresholds thresholds)
        {
            _resourceThresholds = thresholds;
        }

        private void CheckResourceThresholds(ResourceMetrics metrics)
        {
            var alerts = new List<ResourceAlert>();

            if (metrics.CpuUtilization > _resourceThresholds.MaxCpuUtilization)
            {
                alerts.Add(new ResourceAlert
                {
                    ResourceType = ResourceType.Cpu,
                    CurrentUtilization = metrics.CpuUtilization,
                    ThresholdUtilization = _resourceThresholds.MaxCpuUtilization,
                    Severity = AlertSeverity.High,
                    Description = "CPU utilization exceeds threshold"
                });
            }

            if (metrics.MemoryUtilizationPercentage > 
                (_resourceThresholds.MaxMemoryUsage / (double)metrics.TotalMemory * 100))
            {
                alerts.Add(new ResourceAlert
                {
                    ResourceType = ResourceType.Memory,
                    CurrentUtilization = metrics.MemoryUtilizationPercentage,
                    ThresholdUtilization = _resourceThresholds.MaxMemoryUsage,
                    Severity = AlertSeverity.High,
                    Description = "Memory utilization exceeds threshold"
                });
            }

            // Log or notify about alerts
            foreach (var alert in alerts)
            {
                _logger.LogWarning(
                    "Resource Alert: {ResourceType} - {Description}. " +
                    "Current: {CurrentUtilization}, Threshold: {ThresholdUtilization}", 
                    alert.ResourceType, 
                    alert.Description, 
                    alert.CurrentUtilization, 
                    alert.ThresholdUtilization
                );
            }
        }

        private async Task<ResourceMetrics> GetWindowsResourceMetricsAsync()
        {
            try 
            {
                using var process = Process.GetCurrentProcess();
                var performanceCounter = new PerformanceCounter();

                // CPU Metrics
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                var cpuUtilization = cpuCounter.NextValue();

                // Memory Metrics
                var totalMemory = GetTotalPhysicalMemory();
                var availableMemory = GetAvailableMemory();
                var usedMemory = totalMemory - availableMemory;

                return new ResourceMetrics
                {
                    CpuUtilization = cpuUtilization,
                    CpuCoreCount = Environment.ProcessorCount,
                    
                    TotalMemory = totalMemory,
                    UsedMemory = usedMemory,
                    AvailableMemory = availableMemory,
                    MemoryUtilizationPercentage = (usedMemory / (double)totalMemory) * 100,

                    ProcessMemoryUsage = process.WorkingSet64,
                    ProcessCpuUtilization = GetProcessCpuUtilization(process)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error tracking Windows resources: {ex.Message}");
                return new ResourceMetrics();
            }
        }

        private async Task<ResourceMetrics> GetUnixResourceMetricsAsync()
        {
            // Placeholder for Unix-based resource tracking
            // Implement using platform-specific APIs
            return new ResourceMetrics();
        }

        private long GetTotalPhysicalMemory()
        {
            try 
            {
                using var mc = new ManagementClass("Win32_ComputerSystem");
                var moc = mc.GetInstances();

                foreach (ManagementObject item in moc)
                {
                    return Convert.ToInt64(item["TotalPhysicalMemory"]);
                }
            }
            catch { }

            return 0;
        }

        private long GetAvailableMemory()
        {
            try 
            {
                using var mc = new ManagementClass("Win32_OperatingSystem");
                var moc = mc.GetInstances();

                foreach (ManagementObject item in moc)
                {
                    return Convert.ToInt64(item["FreePhysicalMemory"]) * 1024;
                }
            }
            catch { }

            return 0;
        }

        private double GetProcessCpuUtilization(Process process)
        {
            try 
            {
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;

                System.Threading.Thread.Sleep(500);

                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                return cpuUsageTotal * 100;
            }
            catch
            {
                return 0;
            }
        }
    }
}
