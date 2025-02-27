namespace MAria2.Core.Exceptions;

/// <summary>
/// Base exception for dependency update-related errors
/// </summary>
public class DependencyUpdateException : Exception
{
    public DependencyErrorType ErrorType { get; }
    public string DependencyName { get; }

    public DependencyUpdateException(
        string message, 
        DependencyErrorType errorType, 
        string dependencyName,
        Exception innerException = null) 
        : base(message, innerException)
    {
        ErrorType = errorType;
        DependencyName = dependencyName;
    }
}

/// <summary>
/// Exception for download-related dependency update errors
/// </summary>
public class DependencyDownloadException : DependencyUpdateException
{
    public Uri DownloadUri { get; }

    public DependencyDownloadException(
        string dependencyName, 
        Uri downloadUri, 
        string message, 
        Exception innerException = null) 
        : base(
            message, 
            DependencyErrorType.DownloadFailed, 
            dependencyName, 
            innerException)
    {
        DownloadUri = downloadUri;
    }
}

/// <summary>
/// Exception for dependency verification failures
/// </summary>
public class DependencyVerificationFailedException : DependencyUpdateException
{
    public string ExpectedHash { get; }
    public string ActualHash { get; }

    public DependencyVerificationFailedException(
        string dependencyName, 
        string expectedHash, 
        string actualHash) 
        : base(
            "Dependency hash verification failed", 
            DependencyErrorType.VerificationFailed, 
            dependencyName)
    {
        ExpectedHash = expectedHash;
        ActualHash = actualHash;
    }
}

/// <summary>
/// Exception for compatibility-related dependency update errors
/// </summary>
public class DependencyCompatibilityException : DependencyUpdateException
{
    public string[] SupportedArchitectures { get; }
    public string[] SupportedOperatingSystems { get; }

    public DependencyCompatibilityException(
        string dependencyName, 
        string[] supportedArchitectures, 
        string[] supportedOperatingSystems) 
        : base(
            "Dependency is not compatible with current system", 
            DependencyErrorType.IncompatibleSystem, 
            dependencyName)
    {
        SupportedArchitectures = supportedArchitectures;
        SupportedOperatingSystems = supportedOperatingSystems;
    }
}

/// <summary>
/// Enumeration of possible dependency update error types
/// </summary>
public enum DependencyErrorType
{
    Unknown,
    DownloadFailed,
    VerificationFailed,
    IncompatibleSystem,
    InstallationFailed,
    ConfigurationError
}

/// <summary>
/// Extension methods for handling dependency update errors
/// </summary>
public static class DependencyUpdateErrorHandler
{
    /// <summary>
    /// Provides a user-friendly error message based on the exception
    /// </summary>
    public static string GetUserFriendlyMessage(this DependencyUpdateException ex)
    {
        return ex.ErrorType switch
        {
            DependencyErrorType.DownloadFailed => 
                $"Failed to download dependency {ex.DependencyName}. Please check your internet connection.",
            
            DependencyErrorType.VerificationFailed => 
                $"Integrity check failed for {ex.DependencyName}. The download may be corrupted.",
            
            DependencyErrorType.IncompatibleSystem => 
                $"{ex.DependencyName} is not compatible with your current system configuration.",
            
            DependencyErrorType.InstallationFailed => 
                $"Could not install {ex.DependencyName}. Please check system permissions.",
            
            DependencyErrorType.ConfigurationError => 
                $"Configuration error with {ex.DependencyName}. Please review your settings.",
            
            _ => $"An unknown error occurred with {ex.DependencyName}"
        };
    }

    /// <summary>
    /// Determines the severity of the dependency update error
    /// </summary>
    public static ErrorSeverity GetErrorSeverity(this DependencyUpdateException ex)
    {
        return ex.ErrorType switch
        {
            DependencyErrorType.DownloadFailed => ErrorSeverity.High,
            DependencyErrorType.VerificationFailed => ErrorSeverity.Critical,
            DependencyErrorType.IncompatibleSystem => ErrorSeverity.Medium,
            DependencyErrorType.InstallationFailed => ErrorSeverity.High,
            DependencyErrorType.ConfigurationError => ErrorSeverity.Low,
            _ => ErrorSeverity.Unknown
        };
    }
}

/// <summary>
/// Severity levels for dependency update errors
/// </summary>
public enum ErrorSeverity
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}
