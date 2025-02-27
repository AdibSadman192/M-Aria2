using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface IDownloadEngineBenchmark
    {
        /// <summary>
        /// Run comprehensive performance benchmark for a download engine
        /// </summary>
        Task<DownloadEngineBenchmarkResult> RunBenchmarkAsync(
            IDownloadEngine engine, 
            BenchmarkConfiguration configuration);

        /// <summary>
        /// Compare performance across multiple download engines
        /// </summary>
        Task<IEnumerable<DownloadEngineBenchmarkResult>> CompareEnginesAsync(
            IEnumerable<IDownloadEngine> engines, 
            BenchmarkConfiguration configuration);

        /// <summary>
        /// Generate detailed benchmark report
        /// </summary>
        Task<BenchmarkReport> GenerateBenchmarkReportAsync(
            IEnumerable<DownloadEngineBenchmarkResult> benchmarkResults);
    }

    public record BenchmarkConfiguration
    {
        // Test URLs with different characteristics
        public List<string> TestUrls { get; init; } = new List<string>();

        // Download size ranges to test
        public List<long> FileSizes { get; init; } = new List<long>
        {
            1024,           // 1 KB
            1024 * 1024,    // 1 MB
            10 * 1024 * 1024, // 10 MB
            100 * 1024 * 1024 // 100 MB
        };

        // Network conditions to simulate
        public List<NetworkCondition> NetworkConditions { get; init; } = new List<NetworkCondition>
        {
            new NetworkCondition { Bandwidth = 1_000_000, Latency = 50 },   // Good
            new NetworkCondition { Bandwidth = 500_000, Latency = 100 },    // Average
            new NetworkCondition { Bandwidth = 100_000, Latency = 200 }     // Poor
        };

        // Number of times to repeat each test
        public int RepeatCount { get; init; } = 5

        // Timeout for each download attempt
        public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromMinutes(5)
    }

    public record NetworkCondition
    {
        // Bandwidth in bits per second
        public long Bandwidth { get; init; }

        // Latency in milliseconds
        public int Latency { get; init; }
    }

    public record DownloadEngineBenchmarkResult
    {
        public string EngineName { get; init; }
        public string TestUrl { get; init; }
        public long FileSize { get; init; }
        public NetworkCondition NetworkCondition { get; init; }

        // Performance Metrics
        public double AverageDownloadSpeed { get; init; }
        public TimeSpan AverageDownloadTime { get; init; }
        public double SuccessRate { get; init; }
        public long TotalBytesDownloaded { get; init; }

        // Error Metrics
        public int TotalErrorCount { get; init; }
        public List<string> ErrorMessages { get; init; }

        // Resource Utilization
        public double CpuUtilization { get; init; }
        public long MemoryUtilization { get; init; }
        public long NetworkUtilization { get; init; }
    }

    public record BenchmarkReport
    {
        public DateTime BenchmarkDate { get; init; } = DateTime.UtcNow;
        public List<DownloadEngineBenchmarkResult> BenchmarkResults { get; init; }
        public DownloadEngineBenchmarkResult BestOverallEngine { get; init; }
        public List<BenchmarkRecommendation> Recommendations { get; init; }
    }

    public record BenchmarkRecommendation
    {
        public string RecommendationType { get; init; }
        public string Description { get; init; }
        public double Impact { get; init; }
    }
}
