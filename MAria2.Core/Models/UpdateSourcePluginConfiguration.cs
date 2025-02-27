using System.Text.Json.Serialization;

namespace MAria2.Core.Models;

/// <summary>
/// Represents the authentication configuration for an update source plugin
/// </summary>
public class UpdateSourceAuthConfig
{
    /// <summary>
    /// Authentication username
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; }

    /// <summary>
    /// Authentication secret or token
    /// </summary>
    [JsonPropertyName("secret")]
    public string Secret { get; set; }

    /// <summary>
    /// Encrypted credentials for secure storage
    /// </summary>
    [JsonPropertyName("encryptedCredentials")]
    public string EncryptedCredentials { get; set; }

    /// <summary>
    /// Authentication type (e.g., Basic, Bearer, OAuth)
    /// </summary>
    [JsonPropertyName("authType")]
    public string AuthenticationType { get; set; }
}

/// <summary>
/// Represents the configuration for an update source plugin
/// </summary>
public class UpdateSourcePluginConfiguration
{
    /// <summary>
    /// Unique identifier for the plugin configuration
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the update source plugin
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Type of the update source plugin (e.g., GitHub, GitLab, Custom)
    /// </summary>
    [JsonPropertyName("pluginType")]
    public string PluginType { get; set; }

    /// <summary>
    /// Base URL for the update source
    /// </summary>
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }

    /// <summary>
    /// Authentication configuration for the update source
    /// </summary>
    [JsonPropertyName("authentication")]
    public UpdateSourceAuthConfig Authentication { get; set; }

    /// <summary>
    /// List of dependencies supported by this update source
    /// </summary>
    [JsonPropertyName("supportedDependencies")]
    public List<string> SupportedDependencies { get; set; } = new List<string>();

    /// <summary>
    /// Priority of the update source (higher number means higher priority)
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 10;

    /// <summary>
    /// Indicates if the update source is enabled
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Additional metadata or configuration for the update source
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Version range of dependencies supported by this update source
    /// </summary>
    [JsonPropertyName("supportedVersions")]
    public Dictionary<string, string> SupportedVersions { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Last successful update check timestamp
    /// </summary>
    public DateTime? LastSuccessfulUpdateCheck { get; set; }

    /// <summary>
    /// Number of successful updates
    /// </summary>
    public int SuccessfulUpdateCount { get; set; }

    /// <summary>
    /// Number of failed update attempts
    /// </summary>
    public int FailedUpdateCount { get; set; }
}

/// <summary>
/// Enum for update source plugin types
/// </summary>
public enum UpdateSourcePluginType
{
    GitHub,
    GitLab,
    CustomRepository,
    NuGet,
    DockerRegistry
}

/// <summary>
/// Service for managing update source plugin configurations
/// </summary>
public interface IUpdateSourcePluginConfigurationService
{
    /// <summary>
    /// Get all configured update source plugins
    /// </summary>
    Task<List<UpdateSourcePluginConfiguration>> GetConfiguredPluginsAsync();

    /// <summary>
    /// Add a new update source plugin configuration
    /// </summary>
    Task<UpdateSourcePluginConfiguration> AddPluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration);

    /// <summary>
    /// Update an existing update source plugin configuration
    /// </summary>
    Task UpdatePluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration);

    /// <summary>
    /// Remove an update source plugin configuration
    /// </summary>
    Task RemovePluginConfigurationAsync(string pluginId);

    /// <summary>
    /// Validate the configuration of an update source plugin
    /// </summary>
    Task<bool> ValidatePluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration);

    /// <summary>
    /// Test the connection to an update source
    /// </summary>
    Task<bool> TestUpdateSourceConnectionAsync(
        UpdateSourcePluginConfiguration configuration);
}
