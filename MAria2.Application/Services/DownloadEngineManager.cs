using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using MAria2.Core.Models;
using Microsoft.Extensions.Logging;

namespace MAria2.Application.Services
{
    public class DownloadEngineManager : IDownloadEngineManager
    {
        private readonly ILogger<DownloadEngineManager> _logger;
        private readonly IErrorRecoveryService _errorRecoveryService;
        private readonly IPerformanceTrackingService _performanceTracker;

        private readonly ConcurrentDictionary<string, IDownloadEngine> _registeredEngines;
        private readonly ConcurrentDictionary<string, DownloadEnginePerformanceMetrics> _enginePerformance;
        
        private IEngineSelectionStrategy _engineSelectionStrategy;

        public DownloadEngineManager(
            ILogger<DownloadEngineManager> logger,
            IErrorRecoveryService errorRecoveryService,
            IPerformanceTrackingService performanceTracker)
        {
            _logger = logger;
            _errorRecoveryService = errorRecoveryService;
            _performanceTracker = performanceTracker;

            _registeredEngines = new ConcurrentDictionary<string, IDownloadEngine>();
            _enginePerformance = new ConcurrentDictionary<string, DownloadEnginePerformanceMetrics>();
            
            // Default to basic selection strategy
            _engineSelectionStrategy = new DefaultEngineSelectionStrategy();
        }

        public void RegisterEngine(IDownloadEngine engine)
        {
            if (engine == null)
                throw new ArgumentNullException(nameof(engine));

            var engineName = engine.EngineMetadata.EngineName;
            
            _registeredEngines[engineName] = engine;
            _enginePerformance[engineName] = new DownloadEnginePerformanceMetrics
            {
                EngineName = engineName,
                TotalDownloads = 0,
                AverageDownloadSpeed = 0,
                SuccessRate = 0,
                AverageDownloadTime = TimeSpan.Zero,
                TotalBytesDownloaded = 0
            };

            _logger.LogInformation($"Registered download engine: {engineName}");
        }

        public void UnregisterEngine(string engineName)
        {
            _registeredEngines.TryRemove(engineName, out _);
            _enginePerformance.TryRemove(engineName, out _);

            _logger.LogInformation($"Unregistered download engine: {engineName}");
        }

        public async Task<IDownloadEngine> SelectBestEngineAsync(string url)
        {
            var availableEngines = _registeredEngines.Values.ToList();

            if (!availableEngines.Any())
            {
                throw new InvalidOperationException("No download engines are registered");
            }

            return await _engineSelectionStrategy.SelectEngineAsync(availableEngines, url);
        }

        public async Task<DownloadResult> DownloadFileAsync(
            string url, 
            string destinationPath, 
            DownloadPriority priority = DownloadPriority.Normal,
            CancellationToken cancellationToken = default)
        {
            try 
            {
                // Select best engine
                var selectedEngine = await SelectBestEngineAsync(url);
                
                // Track download start time
                var startTime = DateTime.UtcNow;

                // Perform download
                var downloadResult = await selectedEngine.DownloadFileAsync(
                    url, 
                    destinationPath, 
                    cancellationToken
                );

                // Calculate download metrics
                var downloadTime = DateTime.UtcNow - startTime;
                UpdateEnginePerformance(
                    selectedEngine.EngineMetadata.EngineName, 
                    downloadResult, 
                    downloadTime
                );

                // Log successful download
                if (downloadResult.Success)
                {
                    _logger.LogInformation(
                        $"Successfully downloaded {url} using {selectedEngine.EngineMetadata.EngineName}"
                    );
                }
                else 
                {
                    // Log download failure
                    await _errorRecoveryService.LogErrorAsync(new ErrorDetails
                    {
                        ErrorCategory = nameof(ErrorCategory.DownloadFailure),
                        ErrorMessage = $"Download failed for {url}: {downloadResult.ErrorMessage}",
                        SourceComponent = nameof(DownloadEngineManager),
                        Severity = ErrorSeverity.Warning
                    });
                }

                return downloadResult;
            }
            catch (Exception ex)
            {
                // Comprehensive error handling
                await _errorRecoveryService.LogErrorAsync(new ErrorDetails
                {
                    ErrorCategory = nameof(ErrorCategory.DownloadFailure),
                    ErrorMessage = $"Download management failed for {url}: {ex.Message}",
                    SourceComponent = nameof(DownloadEngineManager),
                    Severity = ErrorSeverity.Error,
                    StackTrace = ex.StackTrace
                });

                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<IEnumerable<DownloadEnginePerformanceMetrics>> GetEnginePerformanceMetricsAsync()
        {
            return _enginePerformance.Values.ToList();
        }

        public IEnumerable<IDownloadEngine> GetRegisteredEngines()
        {
            return _registeredEngines.Values;
        }

        public void SetEngineSelectionStrategy(IEngineSelectionStrategy strategy)
        {
            _engineSelectionStrategy = strategy ?? 
                throw new ArgumentNullException(nameof(strategy));
        }

        private void UpdateEnginePerformance(
            string engineName, 
            DownloadResult downloadResult, 
            TimeSpan downloadTime)
        {
            _enginePerformance.AddOrUpdate(
                engineName,
                _ => new DownloadEnginePerformanceMetrics
                {
                    EngineName = engineName,
                    TotalDownloads = 1,
                    AverageDownloadSpeed = downloadResult.BytesDownloaded / downloadTime.TotalSeconds,
                    SuccessRate = downloadResult.Success ? 1.0 : 0.0,
                    AverageDownloadTime = downloadTime,
                    TotalBytesDownloaded = downloadResult.BytesDownloaded
                },
                (_, existingMetrics) => new DownloadEnginePerformanceMetrics
                {
                    EngineName = engineName,
                    TotalDownloads = existingMetrics.TotalDownloads + 1,
                    AverageDownloadSpeed = CalculateMovingAverage(
                        existingMetrics.AverageDownloadSpeed, 
                        downloadResult.BytesDownloaded / downloadTime.TotalSeconds, 
                        existingMetrics.TotalDownloads
                    ),
                    SuccessRate = CalculateMovingAverage(
                        existingMetrics.SuccessRate, 
                        downloadResult.Success ? 1.0 : 0.0, 
                        existingMetrics.TotalDownloads
                    ),
                    AverageDownloadTime = CalculateMovingAverageTimeSpan(
                        existingMetrics.AverageDownloadTime, 
                        downloadTime, 
                        existingMetrics.TotalDownloads
                    ),
                    TotalBytesDownloaded = existingMetrics.TotalBytesDownloaded + downloadResult.BytesDownloaded
                }
            );
        }

        private double CalculateMovingAverage(double currentAverage, double newValue, int totalCount)
        {
            return ((currentAverage * (totalCount - 1)) + newValue) / totalCount;
        }

        private TimeSpan CalculateMovingAverageTimeSpan(TimeSpan currentAverage, TimeSpan newValue, int totalCount)
        {
            var currentTotalSeconds = currentAverage.TotalSeconds * (totalCount - 1);
            var newTotalSeconds = (currentTotalSeconds + newValue.TotalSeconds) / totalCount;
            return TimeSpan.FromSeconds(newTotalSeconds);
        }
    }
}
