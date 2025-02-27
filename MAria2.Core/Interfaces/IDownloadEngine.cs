using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Configuration;

namespace MAria2.Core.Interfaces;

public interface IDownloadEngine
{
    // Unique identifier for the engine
    EngineType Type { get; }
    string Version { get; }
    
    // Core Download Management
    Task<Download> StartDownloadAsync(Download download);
    Task PauseDownloadAsync(Download download);
    Task ResumeDownloadAsync(Download download);
    Task CancelDownloadAsync(Download download);
    Task<DownloadProgress> GetProgressAsync(Download download);

    // Performance and Capability Testing
    bool CanHandleProtocol(string url);
    bool SupportsContentType(string contentType);
    Task<PerformanceTestResult> TestPerformanceAsync(string url);

    // Advanced Download Features
    Task<Download> SplitDownloadAsync(Download download, int segments);
    Task<Download> MergeDownloadSegmentsAsync(Download download);
    
    // Proxy and Network Configuration
    Task ConfigureNetworkAsync(NetworkConfiguration networkConfig);

    // Authentication and Security
    Task<bool> AuthenticateAsync(AuthenticationCredentials credentials);

    // Metadata and Content Analysis
    Task<ContentMetadata> AnalyzeContentAsync(string url);

    // Configuration and Customization
    Task ConfigureEngineAsync(EngineConfiguration config);

    // Lifecycle and Resource Management
    Task InitializeAsync();
    Task ShutdownAsync();
}

// Enhanced performance test result
public record PerformanceTestResult(
    double SpeedMbps, 
    double ConnectionStability, 
    string[] SupportedProtocols,
    TimeSpan AverageLatency,
    int SuccessRate
);

// Network configuration for download engines
public record NetworkConfiguration(
    bool UseProxy = false,
    string ProxyAddress = null,
    string ProxyUsername = null,
    string ProxyPassword = null,
    int ConnectionTimeout = 30,
    int MaxConnections = 5,
    bool UseHttps = true
);

// Authentication credentials
public record AuthenticationCredentials(
    string Username = null,
    string Password = null,
    string Token = null
);

// Detailed content metadata
public record ContentMetadata(
    string Url,
    long FileSize,
    string ContentType,
    string FileName,
    DateTime LastModified,
    string[] AvailableFormats = null,
    Dictionary<string, string> AdditionalMetadata = null
);

// Base configuration for download engines
public record EngineConfiguration(
    int MaxConnections = 5,
    int Timeout = 30,
    bool UseProxy = false,
    string ProxyAddress = null,
    LogLevel LoggingLevel = LogLevel.Information
);

// Download engine capability descriptor
public interface IEngineCapabilityProvider
{
    EngineType EngineType { get; }
    bool SupportsProtocol(string protocol);
    int GetPriority(string url);
    bool CanPartiallyResume(Download download);
}
