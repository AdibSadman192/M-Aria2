using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Management;

namespace MAria2.Application.Services
{
    public class PerformanceTrackingService : IPerformanceTrackingService
    {
        private readonly ILogger<PerformanceTrackingService> _logger;
        private readonly ConcurrentBag<DownloadPerformanceMetric> _downloadMetrics;
        private readonly ConcurrentBag<SystemResourceMetric> _resourceMetrics;
        private readonly IDownloadEngineManager _engineManager;

        public PerformanceTrackingService(
            ILogger<PerformanceTrackingService> logger,
            IDownloadEngineManager engineManager)
        {
            _logger = logger;
            _engineManager = engineManager;
            _downloadMetrics = new ConcurrentBag<DownloadPerformanceMetric>();
            _resourceMetrics = new ConcurrentBag<SystemResourceMetric>();

            // Start background resource monitoring
            StartResourceMonitoring();
        }

        public async Task TrackDownloadPerformanceAsync(DownloadPerformanceMetric metric)
        {
            try 
            {
                _downloadMetrics.Add(metric);

                // Limit historical metrics to prevent memory growth
                while (_downloadMetrics.Count > 10000)
                {
                    _downloadMetrics.TryTake(out _);
                }

                _logger.LogInformation($"Tracked download performance: {metric.Url}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Performance tracking error: {ex.Message}");
            }
        }

        public async Task<IEnumerable<DownloadPerformanceMetric>> GetPerformanceMetricsAsync(
            DateTime startTime, 
            DateTime endTime)
        {
            return _downloadMetrics
                .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
                .OrderBy(m => m.Timestamp);
        }

        public async Task<EnginePerformanceSummary> CalculateEnginePerformanceAsync()
        {
            var engineGroups = _downloadMetrics
                .GroupBy(m => m.DownloadEngine)
                .Select(group => new 
                {
                    EngineName = group.Key,
                    Performance = new EnginePerformanceDetails
                    {
                        AverageDownloadSpeed = group.Average(m => m.DownloadSpeed),
                        SuccessRate = group.Count(m => m.WasSuccessful) / (double)group.Count(),
                        TotalDownloads = group.Count(),
                        AverageDownloadTime = TimeSpan.FromSeconds(
                            group.Average(m => m.DownloadDuration.TotalSeconds))
                    }
                })
                .ToDictionary(x => x.EngineName, x => x.Performance);

            var bestEngine = engineGroups
                .OrderByDescending(x => x.Value.AverageDownloadSpeed)
                .FirstOrDefault().Key;

            return new EnginePerformanceSummary
            {
                EnginePerformance = engineGroups,
                BestPerformingEngine = bestEngine
            };
        }

        public async Task TrackSystemResourceUtilizationAsync(SystemResourceMetric resourceMetric)
        {
            try 
            {
                _resourceMetrics.Add(resourceMetric);

                // Limit historical metrics
                while (_resourceMetrics.Count > 10000)
                {
                    _resourceMetrics.TryTake(out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Resource tracking error: {ex.Message}");
            }
        }

        public async Task<IEnumerable<SystemResourceMetric>> GetSystemResourceHistoryAsync(
            DateTime startTime, 
            DateTime endTime)
        {
            return _resourceMetrics
                .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
                .OrderBy(m => m.Timestamp);
        }

        public async Task<IEnumerable<PerformanceOptimizationRecommendation>> GenerateOptimizationRecommendationsAsync()
        {
            var recommendations = new List<PerformanceOptimizationRecommendation>();

            // Analyze download performance
            var enginePerformance = await CalculateEnginePerformanceAsync();
            var resourceHistory = await GetSystemResourceHistoryAsync(
                DateTime.UtcNow.AddHours(-1), 
                DateTime.UtcNow);

            // Engine optimization recommendations
            foreach (var engine in enginePerformance.EnginePerformance)
            {
                if (engine.Value.SuccessRate < 0.8)
                {
                    recommendations.Add(new PerformanceOptimizationRecommendation
                    {
                        RecommendationType = "DownloadEngine",
                        Description = $"Improve {engine.Key} download engine performance",
                        PriorityLevel = 2,
                        AdditionalDetails = new Dictionary<string, object>
                        {
                            { "CurrentSuccessRate", engine.Value.SuccessRate },
                            { "AverageDownloadSpeed", engine.Value.AverageDownloadSpeed }
                        }
                    });
                }
            }

            // Resource utilization recommendations
            var avgCpuUsage = resourceHistory.Average(r => r.CpuUsagePercent);
            var avgMemoryUsage = resourceHistory.Average(r => 
                (double)r.MemoryUsedBytes / r.TotalMemoryBytes * 100);

            if (avgCpuUsage > 70)
            {
                recommendations.Add(new PerformanceOptimizationRecommendation
                {
                    RecommendationType = "SystemResources",
                    Description = "High CPU usage detected. Consider reducing concurrent downloads.",
                    PriorityLevel = 3,
                    AdditionalDetails = new Dictionary<string, object>
                    {
                        { "AverageCPUUsage", avgCpuUsage }
                    }
                });
            }

            if (avgMemoryUsage > 80)
            {
                recommendations.Add(new PerformanceOptimizationRecommendation
                {
                    RecommendationType = "SystemResources",
                    Description = "High memory usage detected. Optimize memory management.",
                    PriorityLevel = 3,
                    AdditionalDetails = new Dictionary<string, object>
                    {
                        { "AverageMemoryUsage", avgMemoryUsage }
                    }
                });
            }

            return recommendations;
        }

        private void StartResourceMonitoring()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try 
                    {
                        var resourceMetric = await CollectSystemResourceMetricsAsync();
                        await TrackSystemResourceUtilizationAsync(resourceMetric);
                        
                        // Collect metrics every 5 minutes
                        await Task.Delay(TimeSpan.FromMinutes(5));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Resource monitoring error: {ex.Message}");
                    }
                }
            });
        }

        private async Task<SystemResourceMetric> CollectSystemResourceMetricsAsync()
        {
            var process = Process.GetCurrentProcess();
            
            return new SystemResourceMetric
            {
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = GetCpuUsage(),
                MemoryUsedBytes = process.WorkingSet64,
                TotalMemoryBytes = GetTotalPhysicalMemory(),
                DiskReadSpeedMBps = GetDiskReadSpeed(),
                DiskWriteSpeedMBps = GetDiskWriteSpeed(),
                NetworkUploadSpeedMbps = GetNetworkUploadSpeed(),
                NetworkDownloadSpeedMbps = GetNetworkDownloadSpeed()
            };
        }

        // Platform-specific resource collection methods (simplified)
        private double GetCpuUsage()
        {
            var cpuCounter = new PerformanceCounter(
                "Processor", 
                "% Processor Time", 
                "_Total"
            );
            return cpuCounter.NextValue();
        }

        private long GetTotalPhysicalMemory()
        {
            var memoryStatus = new MEMORYSTATUSEX();
            memoryStatus.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(memoryStatus);
            NativeMethods.GlobalMemoryStatusEx(ref memoryStatus);
            return (long)memoryStatus.ullTotalPhys;
        }

        // Placeholder methods for disk and network speed
        private double GetDiskReadSpeed() => 0;
        private double GetDiskWriteSpeed() => 0;
        private double GetNetworkUploadSpeed() => 0;
        private double GetNetworkDownloadSpeed() => 0;

        // Native method declarations
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
        }
    }
}
