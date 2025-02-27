using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Runtime.InteropServices;
using MAria2.Core.Interfaces;
using MAria2.Core;

namespace MAria2.Infrastructure.Services;

public class DependencyUpdateService : IDependencyUpdateService, IDisposable
{
    private readonly ILoggingService _loggingService;
    private readonly IDialogService _dialogService;
    private readonly HttpClient _httpClient;
    private readonly string _updateConfigUrl = "https://raw.githubusercontent.com/YourOrg/MAria2/main/dependency-updates.json";
    private readonly string _libraryBasePath;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<IUpdateSourcePlugin> _updateSourcePlugins;
    private readonly DependencyUpdateLogger _updateLogger;

    public DependencyUpdateService(
        ILoggingService loggingService,
        IDialogService dialogService,
        DependencyUpdateLogger updateLogger,
        IEnumerable<IUpdateSourcePlugin> updateSourcePlugins = null,
        string libraryBasePath = null)
    {
        _loggingService = loggingService;
        _dialogService = dialogService;
        _updateLogger = updateLogger;
        _updateSourcePlugins = updateSourcePlugins?.ToList() ?? 
            new List<IUpdateSourcePlugin> 
            { 
                new GitHubUpdateSourcePlugin() 
            };
        _libraryBasePath = libraryBasePath ?? 
            Path.Combine(AppContext.BaseDirectory, "lib");
        _httpClient = new HttpClient();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task<List<DependencyUpdateInfo>> CheckForUpdatesAsync()
    {
        try 
        {
            var currentDependencies = await LoadCurrentDependenciesAsync();
            var availableUpdates = new List<DependencyUpdateInfo>();

            // Check for updates from all registered plugins
            foreach (var plugin in _updateSourcePlugins)
            {
                var pluginUpdates = await plugin.CheckForUpdatesAsync(currentDependencies);
                availableUpdates.AddRange(pluginUpdates);
            }

            // Log update check results
            _updateLogger.LogUpdateCheck(availableUpdates);

            return availableUpdates;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Update check failed: {ex.Message}");
            return new List<DependencyUpdateInfo>();
        }
    }

    private async Task<List<DependencyConfig>> LoadCurrentDependenciesAsync()
    {
        var configPath = Path.Combine(_libraryBasePath, "dependencies.json");
        if (!File.Exists(configPath))
        {
            return new List<DependencyConfig>();
        }

        var jsonContent = await File.ReadAllTextAsync(configPath);
        return JsonSerializer.Deserialize<List<DependencyConfig>>(jsonContent) 
               ?? new List<DependencyConfig>();
    }

    public async Task<bool> UpdateDependencyAsync(DependencyUpdateInfo updateInfo)
    {
        try 
        {
            // Log update attempt
            _updateLogger.LogUpdateAttempt(updateInfo);

            // Validate update using all registered plugins
            var compatiblePlugin = _updateSourcePlugins
                .FirstOrDefault(p => 
                    p.ValidateUpdateCompatibility(updateInfo)
                );

            if (compatiblePlugin == null)
            {
                throw new DependencyCompatibilityException(
                    updateInfo.Name, 
                    updateInfo.SupportedArchitectures, 
                    updateInfo.SupportedOperatingSystems
                );
            }

            // Perform download and update
            var downloadPath = await DownloadDependencyAsync(updateInfo);

            // Verify download
            if (!await VerifyDownloadedDependencyAsync(downloadPath, updateInfo))
            {
                await _dialogService.ShowErrorAsync(
                    "Download Verification Failed", 
                    $"Failed to verify download for {updateInfo.Name}."
                );
                return false;
            }

            // Install the dependency
            await InstallDependencyAsync(downloadPath, updateInfo);

            // Log successful update
            _updateLogger.LogUpdateSuccess(updateInfo);

            return true;
        }
        catch (DependencyUpdateException ex)
        {
            // Log update failure
            _updateLogger.LogUpdateFailure(updateInfo, ex);
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Dependency update failed: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Update Failed", 
                $"Could not update {updateInfo.Name}."
            );
            return false;
        }
    }

    private async Task<string> DownloadDependencyAsync(DependencyUpdateInfo updateInfo)
    {
        var downloadPath = Path.Combine(
            Path.GetTempPath(), 
            $"{updateInfo.Name}_{updateInfo.Version}_download"
        );

        using var response = await _httpClient.GetAsync(
            updateInfo.DownloadUrl, 
            HttpCompletionOption.ResponseHeadersRead
        );

        response.EnsureSuccessStatusCode();

        using var fs = new FileStream(downloadPath, FileMode.CreateNew);
        await response.Content.CopyToAsync(fs);

        return downloadPath;
    }

    private async Task<bool> VerifyDownloadedDependencyAsync(
        string downloadPath, 
        DependencyUpdateInfo updateInfo)
    {
        // Compute hash of downloaded file
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(downloadPath);
        
        var hashBytes = await sha256.ComputeHashAsync(stream);
        var computedHash = BitConverter.ToString(hashBytes)
            .Replace("-", "")
            .ToLowerInvariant();

        // Compare with expected hash
        return computedHash == updateInfo.ExpectedHash;
    }

    private async Task InstallDependencyAsync(
        string downloadPath, 
        DependencyUpdateInfo updateInfo)
    {
        var targetPath = Path.Combine(
            _libraryBasePath, 
            updateInfo.Name.ToLowerInvariant()
        );

        // Create directory if not exists
        Directory.CreateDirectory(targetPath);

        // Extract or move files
        if (Path.GetExtension(downloadPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(
                downloadPath, 
                targetPath, 
                true
            );
        }
        else 
        {
            // For single file dependencies
            var targetFilePath = Path.Combine(
                targetPath, 
                Path.GetFileName(downloadPath)
            );
            File.Move(downloadPath, targetFilePath, true);
        }

        // Update dependencies configuration
        await UpdateDependencyConfigurationAsync(updateInfo);
    }

    private async Task UpdateDependencyConfigurationAsync(DependencyUpdateInfo updateInfo)
    {
        var configPath = Path.Combine(_libraryBasePath, "dependencies.json");
        var dependencies = await LoadCurrentDependenciesAsync();

        var existingDependency = dependencies
            .FirstOrDefault(d => d.Name.Equals(updateInfo.Name, StringComparison.OrdinalIgnoreCase));

        if (existingDependency != null)
        {
            existingDependency.RequiredRuntimeVersion = updateInfo.Version;
            existingDependency.RelativePath = Path.Combine(
                updateInfo.Name.ToLowerInvariant(), 
                Path.GetFileName(updateInfo.DownloadUrl)
            );
        }
        else 
        {
            dependencies.Add(new DependencyConfig
            {
                Name = updateInfo.Name,
                RelativePath = Path.Combine(
                    updateInfo.Name.ToLowerInvariant(), 
                    Path.GetFileName(updateInfo.DownloadUrl)
                ),
                RequiredRuntimeVersion = updateInfo.Version,
                SupportedArchitectures = updateInfo.SupportedArchitectures
            });
        }

        var jsonContent = JsonSerializer.Serialize(
            dependencies, 
            new JsonSerializerOptions { WriteIndented = true }
        );

        await File.WriteAllTextAsync(configPath, jsonContent);
    }

    private bool IsNewVersionAvailable(string currentVersion, string newVersion)
    {
        try 
        {
            return new Version(newVersion) > new Version(currentVersion);
        }
        catch 
        {
            // Fallback to string comparison if version parsing fails
            return string.Compare(newVersion, currentVersion, StringComparison.Ordinal) > 0;
        }
    }

    public async Task StartPeriodicUpdateCheckAsync(TimeSpan interval)
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try 
            {
                var updates = await CheckForUpdatesAsync();
                
                if (updates.Any())
                {
                    await _dialogService.ShowInfoAsync(
                        "Updates Available", 
                        $"Found {updates.Count} dependency updates."
                    );
                }

                await Task.Delay(interval, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Periodic update check failed: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _httpClient.Dispose();
        _cancellationTokenSource.Dispose();
    }

    // Method to register additional update source plugins
    public void RegisterUpdateSourcePlugin(IUpdateSourcePlugin plugin)
    {
        _updateSourcePlugins.Add(plugin);
    }
}

// Update information for a dependency
public class DependencyUpdateInfo
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string DownloadUrl { get; set; }
    public string ExpectedHash { get; set; }
    public string[] SupportedArchitectures { get; set; }
    public string[] SupportedOperatingSystems { get; set; }
}
