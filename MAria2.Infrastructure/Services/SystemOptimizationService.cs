using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Services
{
    public class SystemOptimizationService : ISystemOptimizationService
    {
        private readonly ILogger<SystemOptimizationService> _logger;
        private readonly IResourceTrackingService _resourceTracker;
        private readonly IAdvancedNotificationService _notificationService;

        public SystemOptimizationService(
            ILogger<SystemOptimizationService> logger,
            IResourceTrackingService resourceTracker,
            IAdvancedNotificationService notificationService)
        {
            _logger = logger;
            _resourceTracker = resourceTracker;
            _notificationService = notificationService;
        }

        public async Task OptimizeSystemResourcesAsync()
        {
            var systemResources = await _resourceTracker.GetCurrentResourceUtilizationAsync();

            var optimizationTasks = new List<Task>
            {
                OptimizeCpuResourcesAsync(systemResources.CpuUtilization),
                OptimizeMemoryResourcesAsync(systemResources.MemoryUtilization),
                OptimizeNetworkResourcesAsync(systemResources.NetworkUtilization)
            };

            await Task.WhenAll(optimizationTasks);
        }

        private async Task OptimizeCpuResourcesAsync(double cpuUtilization)
        {
            if (cpuUtilization > 80)
            {
                _logger.LogWarning($"High CPU Utilization detected: {cpuUtilization}%");

                // Identify and throttle CPU-intensive processes
                var cpuIntensiveProcesses = GetCpuIntensiveProcesses();
                foreach (var process in cpuIntensiveProcesses)
                {
                    try 
                    {
                        process.ProcessorAffinity = (IntPtr)1; // Limit to first core
                        process.PriorityClass = ProcessPriorityClass.BelowNormal;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to optimize process {process.ProcessName}: {ex.Message}");
                    }
                }

                await _notificationService.SendNotificationAsync(new NotificationRequest
                {
                    Title = "CPU Optimization",
                    Message = $"Throttled {cpuIntensiveProcesses.Count} processes due to high CPU utilization",
                    Type = NotificationType.SystemResource,
                    Severity = NotificationSeverity.High
                });
            }
        }

        private async Task OptimizeMemoryResourcesAsync(double memoryUtilization)
        {
            if (memoryUtilization > 85)
            {
                _logger.LogWarning($"High Memory Utilization detected: {memoryUtilization}%");

                // Trigger memory cleanup
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    FlushWindowsMemoryCache();
                }

                // Close least recently used processes
                var memorySortedProcesses = GetMemoryIntensiveProcesses();
                var processesToClose = memorySortedProcesses.Take(3);

                foreach (var process in processesToClose)
                {
                    try 
                    {
                        process.Kill();
                        _logger.LogInformation($"Closed process {process.ProcessName} to free memory");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to close process {process.ProcessName}: {ex.Message}");
                    }
                }

                await _notificationService.SendNotificationAsync(new NotificationRequest
                {
                    Title = "Memory Optimization",
                    Message = $"Closed {processesToClose.Count()} processes to free memory",
                    Type = NotificationType.SystemResource,
                    Severity = NotificationSeverity.High
                });
            }
        }

        private async Task OptimizeNetworkResourcesAsync(double networkUtilization)
        {
            if (networkUtilization > 90)
            {
                _logger.LogWarning($"High Network Utilization detected: {networkUtilization}%");

                // Pause non-critical downloads
                await PauseNonCriticalDownloadsAsync();

                await _notificationService.SendNotificationAsync(new NotificationRequest
                {
                    Title = "Network Optimization",
                    Message = "Paused non-critical downloads due to high network utilization",
                    Type = NotificationType.SystemResource,
                    Severity = NotificationSeverity.High
                });
            }
        }

        private List<Process> GetCpuIntensiveProcesses()
        {
            return Process.GetProcesses()
                .OrderByDescending(p => GetProcessCpuUsage(p))
                .Take(5)
                .ToList();
        }

        private List<Process> GetMemoryIntensiveProcesses()
        {
            return Process.GetProcesses()
                .OrderByDescending(p => p.WorkingSet64)
                .ToList();
        }

        private double GetProcessCpuUsage(Process process)
        {
            var startCpuUsage = process.TotalProcessorTime;
            var startTime = DateTime.UtcNow;

            System.Threading.Thread.Sleep(500);

            var endCpuUsage = process.TotalProcessorTime;
            var endTime = DateTime.UtcNow;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return cpuUsageTotal * 100;
        }

        private void FlushWindowsMemoryCache()
        {
            try 
            {
                // Windows-specific memory flush
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c echo Running memory flush && systeminfo",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Memory cache flush failed: {ex.Message}");
            }
        }

        private async Task PauseNonCriticalDownloadsAsync()
        {
            // Placeholder for download management logic
            // This would interact with download engine to pause non-critical downloads
            await Task.CompletedTask;
        }
    }
}
