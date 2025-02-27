namespace MAria2.Core.Interfaces;

public interface IDependencyUpdateService
{
    /// <summary>
    /// Check for available dependency updates
    /// </summary>
    /// <returns>List of available updates</returns>
    Task<List<DependencyUpdateInfo>> CheckForUpdatesAsync();

    /// <summary>
    /// Update a specific dependency
    /// </summary>
    /// <param name="updateInfo">Update information for the dependency</param>
    /// <returns>True if update was successful, false otherwise</returns>
    Task<bool> UpdateDependencyAsync(DependencyUpdateInfo updateInfo);

    /// <summary>
    /// Start periodic checks for dependency updates
    /// </summary>
    /// <param name="interval">Interval between update checks</param>
    Task StartPeriodicUpdateCheckAsync(TimeSpan interval);
}

/// <summary>
/// Represents update information for a dependency
/// </summary>
public class DependencyUpdateInfo
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string DownloadUrl { get; set; }
    public string ExpectedHash { get; set; }
    public string[] SupportedArchitectures { get; set; }
    public string[] SupportedOperatingSystems { get; set; }
}
