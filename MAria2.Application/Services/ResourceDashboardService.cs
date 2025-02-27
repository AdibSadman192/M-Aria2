using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace MAria2.Application.Services
{
    public class ResourceDashboardService : IResourceDashboardService
    {
        private readonly ILogger<ResourceDashboardService> _logger;
        private readonly IResourceTrackingService _resourceTrackingService;
        private readonly IDownloadEngineManager _downloadEngineManager;
        private readonly MLContext _mlContext;

        private readonly ConcurrentQueue<HistoricalResourceData> _resourceHistory;
        private readonly CancellationTokenSource _updateCancellationSource;

        public ResourceDashboardService(
            ILogger<ResourceDashboardService> logger,
            IResourceTrackingService resourceTrackingService,
            IDownloadEngineManager downloadEngineManager)
        {
            _logger = logger;
            _resourceTrackingService = resourceTrackingService;
            _downloadEngineManager = downloadEngineManager;
            _mlContext = new MLContext();

            _resourceHistory = new ConcurrentQueue<HistoricalResourceData>();
            _updateCancellationSource = new CancellationTokenSource();
        }

        public async Task<ResourceDashboardData> GetCurrentDashboardDataAsync()
        {
            try 
            {
                var resourceMetrics = await _resourceTrackingService.GetCurrentResourceUtilizationAsync();
                var enginePerformance = await _downloadEngineManager.GetEnginePerformanceMetricsAsync();

                return new ResourceDashboardData
                {
                    SystemResources = new SystemResourceMetrics
                    {
                        CpuUtilization = resourceMetrics.CpuUtilization,
                        MemoryUtilization = resourceMetrics.MemoryUtilizationPercentage,
                        NetworkUtilization = resourceMetrics.NetworkUtilizationPercentage,
                        DiskUtilization = resourceMetrics.DiskUtilizationPercentage,
                        SystemTemperature = resourceMetrics.CpuTemperature
                    },
                    DownloadResources = new DownloadResourceMetrics
                    {
                        ActiveDownloads = enginePerformance.Count(),
                        TotalBytesDownloaded = enginePerformance.Sum(e => e.TotalBytesDownloaded),
                        AverageDownloadSpeed = enginePerformance.Average(e => e.AverageDownloadSpeed),
                        DownloadQueueLength = 0 // Placeholder, implement actual queue tracking
                    },
                    PerformanceMetrics = CalculatePerformanceIndicators(resourceMetrics, enginePerformance)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Dashboard data retrieval failed: {ex.Message}");
                return new ResourceDashboardData();
            }
        }

        public async Task<IEnumerable<HistoricalResourceData>> GetHistoricalResourceDataAsync(
            TimeSpan timeRange, 
            ResourceDataGranularity granularity = ResourceDataGranularity.Minute)
        {
            var historicalData = await _resourceTrackingService.GetResourceHistoryAsync(timeRange);

            return historicalData
                .Select(m => new HistoricalResourceData
                {
                    Timestamp = m.Timestamp,
                    CpuUtilization = m.CpuUtilization,
                    MemoryUtilization = m.MemoryUtilizationPercentage,
                    NetworkUtilization = m.NetworkUtilizationPercentage
                })
                .GroupBy(GetGranularityGroupKey(granularity))
                .Select(g => new HistoricalResourceData
                {
                    Timestamp = g.First().Timestamp,
                    CpuUtilization = g.Average(x => x.CpuUtilization),
                    MemoryUtilization = g.Average(x => x.MemoryUtilization),
                    NetworkUtilization = g.Average(x => x.NetworkUtilization)
                })
                .OrderBy(x => x.Timestamp);
        }

        public async IAsyncEnumerable<ResourceDashboardData> SubscribeToResourceUpdatesAsync(
            TimeSpan updateInterval, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                _updateCancellationSource.Token
            ).Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                var dashboardData = await GetCurrentDashboardDataAsync();
                yield return dashboardData;

                await Task.Delay(updateInterval, cancellationToken);
            }
        }

        public async Task<IEnumerable<ResourceAlert>> GetCurrentAlertsAsync()
        {
            var resourceMetrics = await _resourceTrackingService.GetCurrentResourceUtilizationAsync();
            var historicalData = await GetHistoricalResourceDataAsync(TimeSpan.FromHours(1));

            var alerts = new List<ResourceAlert>();

            // CPU Alerts
            if (resourceMetrics.CpuUtilization > 80)
            {
                alerts.Add(new ResourceAlert
                {
                    ResourceType = ResourceType.Cpu,
                    CurrentUtilization = resourceMetrics.CpuUtilization,
                    ThresholdUtilization = 80,
                    Severity = AlertSeverity.High,
                    Description = "High CPU utilization detected"
                });
            }

            // Memory Alerts
            if (resourceMetrics.MemoryUtilizationPercentage > 85)
            {
                alerts.Add(new ResourceAlert
                {
                    ResourceType = ResourceType.Memory,
                    CurrentUtilization = resourceMetrics.MemoryUtilizationPercentage,
                    ThresholdUtilization = 85,
                    Severity = AlertSeverity.High,
                    Description = "High memory utilization detected"
                });
            }

            return alerts;
        }

        public async Task<ResourceUtilizationForecast> PredictResourceUtilizationAsync(
            TimeSpan forecastPeriod)
        {
            var historicalData = await GetHistoricalResourceDataAsync(TimeSpan.FromHours(24));
            var predictionEngine = CreatePredictionEngine(historicalData);

            var forecastPoints = new List<ForecastDataPoint>();
            var startTime = DateTime.UtcNow;

            for (int i = 1; i <= forecastPeriod.TotalMinutes; i++)
            {
                var forecastInput = new ResourceUtilizationData
                {
                    Timestamp = startTime.AddMinutes(i)
                };

                var prediction = predictionEngine.Predict(forecastInput);
                forecastPoints.Add(new ForecastDataPoint
                {
                    Timestamp = forecastInput.Timestamp,
                    PredictedCpuUtilization = prediction.PredictedCpuUtilization,
                    PredictedMemoryUtilization = prediction.PredictedMemoryUtilization
                });
            }

            return new ResourceUtilizationForecast
            {
                ForecastStart = startTime,
                ForecastPeriod = forecastPeriod,
                PredictedUtilization = forecastPoints
            };
        }

        private Func<HistoricalResourceData, object> GetGranularityGroupKey(ResourceDataGranularity granularity)
        {
            return granularity switch
            {
                ResourceDataGranularity.Second => 
                    x => x.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                ResourceDataGranularity.Minute => 
                    x => x.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                ResourceDataGranularity.FiveMinutes => 
                    x => x.Timestamp.ToString("yyyy-MM-dd HH:mm").Substring(0, 15),
                ResourceDataGranularity.Hourly => 
                    x => x.Timestamp.ToString("yyyy-MM-dd HH"),
                ResourceDataGranularity.Daily => 
                    x => x.Timestamp.ToString("yyyy-MM-dd"),
                _ => throw new ArgumentOutOfRangeException(nameof(granularity))
            };
        }

        private PerformanceIndicators CalculatePerformanceIndicators(
            ResourceMetrics resourceMetrics, 
            IEnumerable<DownloadEnginePerformanceMetrics> enginePerformance)
        {
            // Calculate overall system performance score
            double cpuScore = 100 - resourceMetrics.CpuUtilization;
            double memoryScore = 100 - resourceMetrics.MemoryUtilizationPercentage;
            double networkScore = 100 - resourceMetrics.NetworkUtilizationPercentage;

            double overallSystemScore = (cpuScore + memoryScore + networkScore) / 3;

            // Calculate download performance score
            double downloadScore = enginePerformance.Any() 
                ? enginePerformance.Average(e => e.SuccessRate * 100) 
                : 100;

            return new PerformanceIndicators
            {
                OverallSystemPerformanceScore = overallSystemScore,
                DownloadPerformanceScore = downloadScore,
                IsSystemOverloaded = resourceMetrics.CpuUtilization > 80 || 
                                     resourceMetrics.MemoryUtilizationPercentage > 85
            };
        }

        private PredictionEngine<ResourceUtilizationData, ResourceUtilizationPrediction> CreatePredictionEngine(
            IEnumerable<HistoricalResourceData> historicalData)
        {
            // Prepare training data
            var trainingData = historicalData
                .Select(h => new ResourceUtilizationData
                {
                    Timestamp = h.Timestamp,
                    CpuUtilization = h.CpuUtilization,
                    MemoryUtilization = h.MemoryUtilization
                })
                .ToList();

            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // Define ML pipeline
            var pipeline = _mlContext.Transforms.CopyColumns("Label", "CpuUtilization")
                .Append(_mlContext.Transforms.AddFeatures("Features", "Timestamp", "MemoryUtilization"))
                .Append(_mlContext.Regression.Trainers.Sdca());

            // Train the model
            var model = pipeline.Fit(dataView);

            // Create prediction engine
            return _mlContext.Model.CreatePredictionEngine<ResourceUtilizationData, ResourceUtilizationPrediction>(model);
        }

        // ML.NET model classes
        private class ResourceUtilizationData
        {
            [LoadColumn(0)]
            public DateTime Timestamp { get; set; }

            [LoadColumn(1)]
            public double CpuUtilization { get; set; }

            [LoadColumn(2)]
            public double MemoryUtilization { get; set; }
        }

        private class ResourceUtilizationPrediction
        {
            [ColumnName("Score")]
            public float PredictedCpuUtilization { get; set; }

            [ColumnName("Score")]
            public float PredictedMemoryUtilization { get; set; }
        }
    }
}
