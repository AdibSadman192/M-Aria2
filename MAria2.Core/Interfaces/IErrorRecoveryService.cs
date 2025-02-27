using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface IErrorRecoveryService
    {
        /// <summary>
        /// Logs a detailed error occurrence
        /// </summary>
        Task LogErrorAsync(ErrorDetails errorDetails);

        /// <summary>
        /// Attempts to recover from a specific error
        /// </summary>
        Task<ErrorRecoveryResult> RecoverFromErrorAsync(ErrorDetails errorDetails);

        /// <summary>
        /// Retrieves recent error logs
        /// </summary>
        Task<IEnumerable<ErrorDetails>> GetRecentErrorLogsAsync(TimeSpan timeRange);

        /// <summary>
        /// Analyzes error patterns and generates recommendations
        /// </summary>
        Task<ErrorAnalysisSummary> AnalyzeErrorPatternsAsync();

        /// <summary>
        /// Creates a circuit breaker for repeated errors
        /// </summary>
        Task<bool> ActivateCircuitBreakerAsync(string errorCategory);

        /// <summary>
        /// Generates a comprehensive error report
        /// </summary>
        Task<ErrorReport> GenerateErrorReportAsync();
    }

    public record ErrorDetails
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public ErrorSeverity Severity { get; init; }
        public string ErrorCategory { get; init; }
        public string ErrorMessage { get; init; }
        public string StackTrace { get; init; }
        public string SourceComponent { get; init; }
        public Dictionary<string, string> AdditionalContext { get; init; }
    }

    public record ErrorRecoveryResult
    {
        public bool WasRecoverySuccessful { get; init; }
        public string RecoveryAction { get; init; }
        public string RecoveryDescription { get; init; }
    }

    public record ErrorAnalysisSummary
    {
        public int TotalErrorCount { get; init; }
        public Dictionary<string, int> ErrorCountByCategory { get; init; }
        public Dictionary<string, double> ErrorRateByComponent { get; init; }
        public List<ErrorRecommendation> Recommendations { get; init; }
    }

    public record ErrorRecommendation
    {
        public string RecommendationType { get; init; }
        public string Description { get; init; }
        public ErrorSeverity Severity { get; init; }
    }

    public record ErrorReport
    {
        public DateTime ReportGeneratedAt { get; init; } = DateTime.UtcNow;
        public ErrorAnalysisSummary AnalysisSummary { get; init; }
        public IEnumerable<ErrorDetails> RecentErrors { get; init; }
        public List<ErrorRecoveryStrategy> RecommendedStrategies { get; init; }
    }

    public record ErrorRecoveryStrategy
    {
        public string StrategyName { get; init; }
        public string Description { get; init; }
        public List<string> RecoverySteps { get; init; }
    }

    public enum ErrorSeverity
    {
        Informational,
        Warning,
        Error,
        Critical
    }

    public enum ErrorCategory
    {
        NetworkError,
        DownloadFailure,
        AuthenticationError,
        ResourceConstraint,
        ConfigurationError,
        SystemError,
        Unknown
    }
}
