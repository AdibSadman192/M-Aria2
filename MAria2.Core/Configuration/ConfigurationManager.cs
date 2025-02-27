using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using MAria2.Core.Interfaces;
using MAria2.Core.Enums;

namespace MAria2.Core.Configuration;

/// <summary>
/// Centralized configuration management with advanced features
/// </summary>
public class ConfigurationManager : IConfigurationService
{
    private readonly string _configPath;
    private readonly ConcurrentDictionary<string, object> _cachedConfigurations = new();
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILoggingService _loggingService;

    // Global application settings
    public GlobalSettings GlobalSettings { get; private set; }

    // Engine-specific configurations
    public ConcurrentDictionary<EngineType, EngineConfiguration> EngineConfigurations { get; } = new();

    public ConfigurationManager(
        ILoggingService loggingService, 
        string configPath = null)
    {
        _loggingService = loggingService;
        _configPath = configPath ?? 
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "MAria2", 
                "config.json"
            );

        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        EnsureConfigDirectoryExists();
        LoadConfigurations();
    }

    private void EnsureConfigDirectoryExists()
    {
        try 
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to create config directory: {ex.Message}");
            throw;
        }
    }

    private void LoadConfigurations()
    {
        try 
        {
            if (!File.Exists(_configPath))
            {
                // Create default configuration if not exists
                GlobalSettings = new GlobalSettings();
                SaveConfigurations();
                return;
            }

            var jsonContent = File.ReadAllText(_configPath);
            var rootConfig = JsonSerializer.Deserialize<RootConfiguration>(
                jsonContent, 
                _serializerOptions
            );

            GlobalSettings = rootConfig.GlobalSettings ?? new GlobalSettings();
            
            // Load engine-specific configurations
            foreach (var engineConfig in rootConfig.EngineConfigurations)
            {
                EngineConfigurations[engineConfig.Key] = engineConfig.Value;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Configuration load failed: {ex.Message}");
            GlobalSettings = new GlobalSettings();
        }
    }

    public void SaveConfigurations()
    {
        try 
        {
            var rootConfig = new RootConfiguration
            {
                GlobalSettings = GlobalSettings,
                EngineConfigurations = EngineConfigurations.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value
                )
            };

            var jsonContent = JsonSerializer.Serialize(
                rootConfig, 
                _serializerOptions
            );

            File.WriteAllText(_configPath, jsonContent);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Configuration save failed: {ex.Message}");
        }
    }

    public T GetConfiguration<T>(string key) where T : class
    {
        return _cachedConfigurations.TryGetValue(key, out var config) 
            ? config as T 
            : null;
    }

    public void SetConfiguration<T>(string key, T configuration) where T : class
    {
        _cachedConfigurations[key] = configuration;
        SaveConfigurations();
    }

    public EngineConfiguration GetEngineConfiguration(EngineType engineType)
    {
        return EngineConfigurations.TryGetValue(engineType, out var config)
            ? config
            : new EngineConfiguration { Type = engineType };
    }

    public void UpdateEngineConfiguration(
        EngineType engineType, 
        Action<EngineConfiguration> updateAction)
    {
        var config = GetEngineConfiguration(engineType);
        updateAction(config);
        EngineConfigurations[engineType] = config;
        SaveConfigurations();
    }

    // Configuration reset and management
    public void ResetToDefaults()
    {
        GlobalSettings = new GlobalSettings();
        EngineConfigurations.Clear();
        SaveConfigurations();
    }
}

// Root configuration structure
public class RootConfiguration
{
    public GlobalSettings GlobalSettings { get; set; }
    public Dictionary<EngineType, EngineConfiguration> EngineConfigurations { get; set; } = new();
}

// Global application settings
public class GlobalSettings
{
    public int MaxConcurrentDownloads { get; set; } = 5;
    public bool AutoUpdateEnabled { get; set; } = true;
    public string DefaultDownloadDirectory { get; set; } = 
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            "Downloads", 
            "MAria2"
        );
    public LogLevel LoggingLevel { get; set; } = LogLevel.Information;
    public bool AnonymousUsageStatistics { get; set; } = true;
}

// Engine-specific configuration
public class EngineConfiguration
{
    public EngineType Type { get; set; }
    public bool Enabled { get; set; } = true;
    public int MaxConcurrentDownloadsPerEngine { get; set; } = 3;
    public Dictionary<string, string> EngineSpecificSettings { get; set; } = new();
    
    // Performance and retry settings
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryBackoffBase { get; set; } = TimeSpan.FromSeconds(5);
    
    // Bandwidth and connection settings
    public int MaxDownloadSpeedKbps { get; set; } = 0; // Unlimited
    public int ConnectionTimeout { get; set; } = 30; // seconds
}

// Logging level enum
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
