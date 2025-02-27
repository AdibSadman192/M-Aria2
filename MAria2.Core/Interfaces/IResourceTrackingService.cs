using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface IResourceTrackingService
    {
        /// <summary>
        /// Start tracking system resources for a specific operation
        /// </summary>
        Task<ResourceTrackingContext> StartTrackingAsync(string operationId);

        /// <summary>
        /// Stop tracking resources and get final metrics
        /// </summary>
        Task<ResourceMetrics> StopTrackingAsync(ResourceTrackingContext context);

        /// <summary>
        /// Get real-time resource utilization
        /// </summary>
        Task<ResourceMetrics> GetCurrentResourceUtilizationAsync();

        /// <summary>
        /// Log resource utilization history
        /// </summary>
        Task LogResourceHistoryAsync(ResourceMetrics metrics);

        /// <summary>
        /// Get resource utilization history
        /// </summary>
        Task<IEnumerable<ResourceMetrics>> GetResourceHistoryAsync(
            TimeSpan? timeRange = null, 
            ResourceType? resourceType = null);

        /// <summary>
        /// Set resource utilization thresholds for alerts
        /// </summary>
        void SetResourceThresholds(ResourceThresholds thresholds);
    }

    public record ResourceTrackingContext
    {
        public string OperationId { get; init; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; init; } = DateTime.UtcNow;
    }

    public record ResourceMetrics
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string OperationId { get; init; }
        
        // CPU Metrics
        public double CpuUtilization { get; init; }
        public int CpuCoreCount { get; init; }
        public double[] PerCoreCpuUtilization { get; init; }

        // Memory Metrics
        public long TotalMemory { get; init; }
        public long UsedMemory { get; init; }
        public long AvailableMemory { get; init; }
        public double MemoryUtilizationPercentage { get; init; }

        // Network Metrics
        public long NetworkBytesReceived { get; init; }
        public long NetworkBytesSent { get; init; }
        public double NetworkUtilizationPercentage { get; init; }

        // Disk Metrics
        public long DiskReadBytes { get; init; }
        public long DiskWriteBytes { get; init; }
        public double DiskUtilizationPercentage { get; init; }

        // Process-Specific Metrics
        public long ProcessMemoryUsage { get; init; }
        public double ProcessCpuUtilization { get; init; }

        // Thermal and Power
        public double CpuTemperature { get; init; }
        public double SystemPowerConsumption { get; init; }
    }

    public record ResourceThresholds
    {
        public double MaxCpuUtilization { get; init; } = 80.0;
        public long MaxMemoryUsage { get; init; } = 16_000_000_000; // 16 GB
        public double MaxNetworkUtilization { get; init; } = 90.0;
        public double MaxDiskUtilization { get; init; } = 90.0;
        public double MaxCpuTemperature { get; init; } = 80.0; // Celsius
    }

    public enum ResourceType
    {
        Cpu,
        Memory,
        Network,
        Disk,
        Power
    }

    public record ResourceAlert
    {
        public ResourceType ResourceType { get; init; }
        public double CurrentUtilization { get; init; }
        public double ThresholdUtilization { get; init; }
        public AlertSeverity Severity { get; init; }
        public string Description { get; init; }
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
