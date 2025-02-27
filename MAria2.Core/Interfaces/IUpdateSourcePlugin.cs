using MAria2.Core.Models;
using MAria2.Core.Enums;

namespace MAria2.Core.Interfaces;

/// <summary>
/// Represents a plugin for managing updates from a specific source
/// </summary>
public interface IUpdateSourcePlugin
{
    /// <summary>
    /// Unique identifier for the update source plugin
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Name of the update source plugin
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Type of update source plugin
    /// </summary>
    UpdateSourceType Type { get; }

    /// <summary>
    /// Check if the plugin supports a specific dependency
    /// </summary>
    /// <param name="dependencyName">Name of the dependency</param>
    /// <param name="version">Optional version to check support for</param>
    /// <returns>True if the dependency is supported, false otherwise</returns>
    bool SupportsDependency(string dependencyName, string version = null);

    /// <summary>
    /// Retrieve available updates for a specific dependency
    /// </summary>
    /// <param name="dependencyName">Name of the dependency</param>
    /// <param name="currentVersion">Current version of the dependency</param>
    /// <returns>List of available updates</returns>
    Task<List<DependencyUpdate>> GetAvailableUpdatesAsync(
        string dependencyName, 
        string currentVersion
    );

    /// <summary>
    /// Download an update for a specific dependency
    /// </summary>
    /// <param name="dependencyName">Name of the dependency</param>
    /// <param name="version">Version to download</param>
    /// <returns>Path to the downloaded update</returns>
    Task<string> DownloadUpdateAsync(
        string dependencyName, 
        string version
    );

    /// <summary>
    /// Validate the integrity of a downloaded update
    /// </summary>
    /// <param name="updatePath">Path to the downloaded update</param>
    /// <returns>True if the update is valid, false otherwise</returns>
    Task<bool> ValidateUpdateAsync(string updatePath);
}

/// <summary>
/// Represents a configuration for an update source plugin
/// </summary>
public interface IUpdateSourcePluginConfigurationService
{
    /// <summary>
    /// Retrieve all configured update source plugins
    /// </summary>
    /// <returns>List of configured update source plugins</returns>
    Task<List<UpdateSourcePluginConfiguration>> GetConfiguredPluginsAsync();

    /// <summary>
    /// Add a new update source plugin configuration
    /// </summary>
    /// <param name="configuration">Configuration to add</param>
    /// <returns>Added configuration</returns>
    Task<UpdateSourcePluginConfiguration> AddPluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration
    );

    /// <summary>
    /// Update an existing update source plugin configuration
    /// </summary>
    /// <param name="configuration">Configuration to update</param>
    Task UpdatePluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration
    );

    /// <summary>
    /// Remove an update source plugin configuration
    /// </summary>
    /// <param name="pluginId">ID of the plugin to remove</param>
    Task RemovePluginConfigurationAsync(string pluginId);

    /// <summary>
    /// Validate a plugin configuration
    /// </summary>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidatePluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration
    );

    /// <summary>
    /// Test connection to an update source
    /// </summary>
    /// <param name="configuration">Configuration to test</param>
    /// <returns>True if connection successful, false otherwise</returns>
    Task<bool> TestUpdateSourceConnectionAsync(
        UpdateSourcePluginConfiguration configuration
    );
}

/// <summary>
/// Represents an update for a specific dependency
/// </summary>
public class DependencyUpdate
{
    /// <summary>
    /// Name of the dependency
    /// </summary>
    public string DependencyName { get; set; }

    /// <summary>
    /// New version of the dependency
    /// </summary>
    public string NewVersion { get; set; }

    /// <summary>
    /// Download URL for the update
    /// </summary>
    public string DownloadUrl { get; set; }

    /// <summary>
    /// Checksum for verifying the update
    /// </summary>
    public string Checksum { get; set; }

    /// <summary>
    /// Checksum algorithm (e.g., MD5, SHA256)
    /// </summary>
    public string ChecksumAlgorithm { get; set; }

    /// <summary>
    /// Release notes or changelog
    /// </summary>
    public string ReleaseNotes { get; set; }

    /// <summary>
    /// Release date of the update
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Indicates if the update is a critical security update
    /// </summary>
    public bool IsCriticalUpdate { get; set; }
}

/// <summary>
/// Enum representing different types of update sources
/// </summary>
public enum UpdateSourceType
{
    GitHub,
    GitLab,
    NuGet,
    Custom
}

/// <summary>
/// Base implementation for update source plugins
/// </summary>
public abstract class UpdateSourcePluginBase : IUpdateSourcePlugin
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract UpdateSourceType Type { get; }

    public abstract bool SupportsDependency(string dependencyName, string version = null);

    public abstract Task<List<DependencyUpdate>> GetAvailableUpdatesAsync(
        string dependencyName, 
        string currentVersion
    );

    public abstract Task<string> DownloadUpdateAsync(
        string dependencyName, 
        string version
    );

    public abstract Task<bool> ValidateUpdateAsync(string updatePath);
}

/// <summary>
/// Plugin for GitHub-based update sources
/// </summary>
public class GitHubUpdateSourcePlugin : UpdateSourcePluginBase
{
    private readonly HttpClient _httpClient;
    private readonly string _githubApiBaseUrl = "https://api.github.com";

    public override string Id => "github";
    public override string Name => "GitHub Releases";
    public override UpdateSourceType Type => UpdateSourceType.GitHub;

    public GitHubUpdateSourcePlugin(HttpClient httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add(
            "User-Agent", 
            "MAria2-UpdatePlugin"
        );
    }

    public override bool SupportsDependency(string dependencyName, string version = null)
    {
        // Mapping dependency names to GitHub repositories
        var repoMapping = new Dictionary<string, string>
        {
            { "Aria2", "aria2/aria2" },
            { "YtDlp", "yt-dlp/yt-dlp" },
            { "Wget", "mirror/wget" }
        };

        return repoMapping.ContainsKey(dependencyName);
    }

    public override async Task<List<DependencyUpdate>> GetAvailableUpdatesAsync(
        string dependencyName, 
        string currentVersion)
    {
        try 
        {
            // Mapping dependency names to GitHub repositories
            var repoMapping = new Dictionary<string, string>
            {
                { "Aria2", "aria2/aria2" },
                { "YtDlp", "yt-dlp/yt-dlp" },
                { "Wget", "mirror/wget" }
            };

            if (!repoMapping.TryGetValue(dependencyName, out var repoFullName))
            {
                return new List<DependencyUpdate>(); // No mapping found
            }

            var url = $"{_githubApiBaseUrl}/repos/{repoFullName}/releases/latest";
            var response = await _httpClient.GetFromJsonAsync<GitHubReleaseInfo>(url);

            if (response == null || string.IsNullOrEmpty(response.TagName))
            {
                return new List<DependencyUpdate>();
            }

            // Remove 'v' prefix if present
            var latestVersion = response.TagName.TrimStart('v');

            // Compare versions
            if (IsNewVersionAvailable(currentVersion, latestVersion))
            {
                return new List<DependencyUpdate>
                {
                    new DependencyUpdate
                    {
                        DependencyName = dependencyName,
                        NewVersion = latestVersion,
                        DownloadUrl = FindBestDownloadAsset(response.Assets)?.DownloadUrl,
                        SupportedArchitectures = new[] { "X64" },
                        SupportedOperatingSystems = new[] { "Windows" }
                    }
                };
            }

            return new List<DependencyUpdate>();
        }
        catch 
        {
            return new List<DependencyUpdate>();
        }
    }

    public override async Task<string> DownloadUpdateAsync(
        string dependencyName, 
        string version)
    {
        try 
        {
            // Mapping dependency names to GitHub repositories
            var repoMapping = new Dictionary<string, string>
            {
                { "Aria2", "aria2/aria2" },
                { "YtDlp", "yt-dlp/yt-dlp" },
                { "Wget", "mirror/wget" }
            };

            if (!repoMapping.TryGetValue(dependencyName, out var repoFullName))
            {
                return null; // No mapping found
            }

            var url = $"{_githubApiBaseUrl}/repos/{repoFullName}/releases/tags/{version}";
            var response = await _httpClient.GetFromJsonAsync<GitHubReleaseInfo>(url);

            if (response == null || string.IsNullOrEmpty(response.TagName))
            {
                return null;
            }

            var downloadUrl = FindBestDownloadAsset(response.Assets)?.DownloadUrl;

            if (string.IsNullOrEmpty(downloadUrl))
            {
                return null;
            }

            var downloadResponse = await _httpClient.GetAsync(downloadUrl);
            var downloadPath = Path.GetTempFileName();

            using (var fileStream = new FileStream(downloadPath, FileMode.Create))
            {
                await downloadResponse.Content.CopyToAsync(fileStream);
            }

            return downloadPath;
        }
        catch 
        {
            return null;
        }
    }

    public override async Task<bool> ValidateUpdateAsync(string updatePath)
    {
        try 
        {
            // TO DO: Implement update validation logic
            return true;
        }
        catch 
        {
            return false;
        }
    }

    private bool IsNewVersionAvailable(string currentVersion, string newVersion)
    {
        try 
        {
            return new Version(newVersion) > new Version(currentVersion);
        }
        catch 
        {
            // Fallback to string comparison
            return string.Compare(newVersion, currentVersion, StringComparison.Ordinal) > 0;
        }
    }

    private GitHubReleaseAsset FindBestDownloadAsset(List<GitHubReleaseAsset> assets)
    {
        // Prioritize Windows 64-bit executables or archives
        return assets
            .Where(a => 
                a.Name.Contains("win64", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("windows", StringComparison.OrdinalIgnoreCase)
            )
            .OrderByDescending(a => a.Size)
            .FirstOrDefault();
    }

    private class GitHubReleaseInfo
    {
        public string TagName { get; set; }
        public List<GitHubReleaseAsset> Assets { get; set; }
    }

    private class GitHubReleaseAsset
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
    }
}
