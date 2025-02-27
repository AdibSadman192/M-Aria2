using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAria2.Application.Services
{
    public class ErrorRecoveryService : IErrorRecoveryService
    {
        private readonly ILogger<ErrorRecoveryService> _logger;
        private readonly ConcurrentBag<ErrorDetails> _errorLogs;
        private readonly ConcurrentDictionary<string, int> _errorCountByCategory;
        private readonly ConcurrentDictionary<string, DateTime> _circuitBreakers;

        public ErrorRecoveryService(ILogger<ErrorRecoveryService> logger)
        {
            _logger = logger;
            _errorLogs = new ConcurrentBag<ErrorDetails>();
            _errorCountByCategory = new ConcurrentDictionary<string, int>();
            _circuitBreakers = new ConcurrentDictionary<string, DateTime>();
        }

        public async Task LogErrorAsync(ErrorDetails errorDetails)
        {
            try 
            {
                // Add to error logs
                _errorLogs.Add(errorDetails);

                // Track error count by category
                _errorCountByCategory.AddOrUpdate(
                    errorDetails.ErrorCategory, 
                    1, 
                    (_, count) => count + 1
                );

                // Log to system logger
                LogErrorToSystemLogger(errorDetails);

                // Limit error log size
                while (_errorLogs.Count > 10000)
                {
                    _errorLogs.TryTake(out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error logging failed: {ex.Message}");
            }
        }

        public async Task<ErrorRecoveryResult> RecoverFromErrorAsync(ErrorDetails errorDetails)
        {
            try 
            {
                // Check if circuit breaker is active
                if (await IsCircuitBreakerActiveAsync(errorDetails.ErrorCategory))
                {
                    return new ErrorRecoveryResult
                    {
                        WasRecoverySuccessful = false,
                        RecoveryAction = "CircuitBreaker",
                        RecoveryDescription = "Error category is temporarily blocked"
                    };
                }

                // Implement recovery strategies based on error category
                return errorDetails.ErrorCategory switch
                {
                    nameof(ErrorCategory.NetworkError) => 
                        await RecoverFromNetworkErrorAsync(errorDetails),
                    nameof(ErrorCategory.DownloadFailure) => 
                        await RecoverFromDownloadFailureAsync(errorDetails),
                    nameof(ErrorCategory.AuthenticationError) => 
                        await RecoverFromAuthenticationErrorAsync(errorDetails),
                    nameof(ErrorCategory.ResourceConstraint) => 
                        await RecoverFromResourceConstraintAsync(errorDetails),
                    _ => new ErrorRecoveryResult
                    {
                        WasRecoverySuccessful = false,
                        RecoveryAction = "NoRecoveryStrategy",
                        RecoveryDescription = "No specific recovery strategy found"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Recovery process failed: {ex.Message}");
                return new ErrorRecoveryResult
                {
                    WasRecoverySuccessful = false,
                    RecoveryAction = "RecoveryFailure",
                    RecoveryDescription = "Unexpected error during recovery"
                };
            }
        }

        public async Task<IEnumerable<ErrorDetails>> GetRecentErrorLogsAsync(TimeSpan timeRange)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeRange);
            return _errorLogs
                .Where(error => error.Timestamp >= cutoffTime)
                .OrderByDescending(error => error.Timestamp);
        }

        public async Task<ErrorAnalysisSummary> AnalyzeErrorPatternsAsync()
        {
            var recentErrors = await GetRecentErrorLogsAsync(TimeSpan.FromHours(24));

            return new ErrorAnalysisSummary
            {
                TotalErrorCount = recentErrors.Count(),
                ErrorCountByCategory = _errorCountByCategory.ToDictionary(
                    x => x.Key, 
                    x => x.Value
                ),
                ErrorRateByComponent = recentErrors
                    .GroupBy(e => e.SourceComponent)
                    .ToDictionary(
                        g => g.Key, 
                        g => (double)g.Count() / recentErrors.Count()
                    ),
                Recommendations = GenerateErrorRecommendations(recentErrors)
            };
        }

        public async Task<bool> ActivateCircuitBreakerAsync(string errorCategory)
        {
            // Activate circuit breaker for 5 minutes
            _circuitBreakers[errorCategory] = DateTime.UtcNow.AddMinutes(5);
            return true;
        }

        public async Task<ErrorReport> GenerateErrorReportAsync()
        {
            var analysisSummary = await AnalyzeErrorPatternsAsync();
            var recentErrors = await GetRecentErrorLogsAsync(TimeSpan.FromHours(24));

            return new ErrorReport
            {
                AnalysisSummary = analysisSummary,
                RecentErrors = recentErrors,
                RecommendedStrategies = GenerateRecoveryStrategies(analysisSummary)
            };
        }

        private async Task<bool> IsCircuitBreakerActiveAsync(string errorCategory)
        {
            if (_circuitBreakers.TryGetValue(errorCategory, out var blockUntil))
            {
                return blockUntil > DateTime.UtcNow;
            }
            return false;
        }

        private async Task<ErrorRecoveryResult> RecoverFromNetworkErrorAsync(ErrorDetails errorDetails)
        {
            // Implement network-specific recovery logic
            return new ErrorRecoveryResult
            {
                WasRecoverySuccessful = true,
                RecoveryAction = "NetworkRetry",
                RecoveryDescription = "Attempting network reconnection"
            };
        }

        private async Task<ErrorRecoveryResult> RecoverFromDownloadFailureAsync(ErrorDetails errorDetails)
        {
            // Implement download-specific recovery logic
            return new ErrorRecoveryResult
            {
                WasRecoverySuccessful = true,
                RecoveryAction = "DownloadRetry",
                RecoveryDescription = "Retrying download with alternative engine"
            };
        }

        private async Task<ErrorRecoveryResult> RecoverFromAuthenticationErrorAsync(ErrorDetails errorDetails)
        {
            // Implement authentication-specific recovery logic
            return new ErrorRecoveryResult
            {
                WasRecoverySuccessful = false,
                RecoveryAction = "AuthenticationReset",
                RecoveryDescription = "Requires manual authentication review"
            };
        }

        private async Task<ErrorRecoveryResult> RecoverFromResourceConstraintAsync(ErrorDetails errorDetails)
        {
            // Implement resource constraint recovery logic
            return new ErrorRecoveryResult
            {
                WasRecoverySuccessful = true,
                RecoveryAction = "ResourceThrottling",
                RecoveryDescription = "Reducing concurrent operations"
            };
        }

        private List<ErrorRecommendation> GenerateErrorRecommendations(
            IEnumerable<ErrorDetails> recentErrors)
        {
            var recommendations = new List<ErrorRecommendation>();

            // Analyze error patterns and generate recommendations
            var criticalErrorCount = recentErrors
                .Count(e => e.Severity == ErrorSeverity.Critical);

            if (criticalErrorCount > 10)
            {
                recommendations.Add(new ErrorRecommendation
                {
                    RecommendationType = "SystemStability",
                    Description = "High number of critical errors detected. Immediate investigation required.",
                    Severity = ErrorSeverity.Critical
                });
            }

            return recommendations;
        }

        private List<ErrorRecoveryStrategy> GenerateRecoveryStrategies(
            ErrorAnalysisSummary analysisSummary)
        {
            var strategies = new List<ErrorRecoveryStrategy>();

            foreach (var category in analysisSummary.ErrorCountByCategory)
            {
                strategies.Add(new ErrorRecoveryStrategy
                {
                    StrategyName = $"{category.Key}RecoveryStrategy",
                    Description = $"Recovery plan for {category.Key} errors",
                    RecoverySteps = new List<string>
                    {
                        "Identify root cause",
                        "Implement targeted fix",
                        "Test and validate solution"
                    }
                });
            }

            return strategies;
        }

        private void LogErrorToSystemLogger(ErrorDetails errorDetails)
        {
            switch (errorDetails.Severity)
            {
                case ErrorSeverity.Informational:
                    _logger.LogInformation(
                        "Error in {Component}: {Message}", 
                        errorDetails.SourceComponent, 
                        errorDetails.ErrorMessage);
                    break;
                case ErrorSeverity.Warning:
                    _logger.LogWarning(
                        "Warning in {Component}: {Message}", 
                        errorDetails.SourceComponent, 
                        errorDetails.ErrorMessage);
                    break;
                case ErrorSeverity.Error:
                    _logger.LogError(
                        "Error in {Component}: {Message}\nStack Trace: {StackTrace}", 
                        errorDetails.SourceComponent, 
                        errorDetails.ErrorMessage, 
                        errorDetails.StackTrace);
                    break;
                case ErrorSeverity.Critical:
                    _logger.LogCritical(
                        "CRITICAL Error in {Component}: {Message}\nStack Trace: {StackTrace}", 
                        errorDetails.SourceComponent, 
                        errorDetails.ErrorMessage, 
                        errorDetails.StackTrace);
                    break;
            }
        }
    }
}
