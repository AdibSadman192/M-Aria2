using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Headers;
using MAria2.Core.Models;
using MAria2.Core.Interfaces;
using MAria2.Core.Enums;

namespace MAria2.Infrastructure.Services;

public class UpdateSourcePluginConfigurationService : IUpdateSourcePluginConfigurationService
{
    private readonly string _configFilePath;
    private readonly ILoggingService _loggingService;
    private readonly IDialogService _dialogService;
    private readonly HttpClient _httpClient;

    // Encryption key for sensitive data (in a real-world scenario, use secure key management)
    private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("M-Aria2SecureUpdateSourceKey12345");
    private static readonly byte[] EncryptionIV = Encoding.UTF8.GetBytes("M-Aria2UpdateIV");

    public UpdateSourcePluginConfigurationService(
        ILoggingService loggingService,
        IDialogService dialogService,
        string configPath = null)
    {
        _loggingService = loggingService;
        _dialogService = dialogService;
        _httpClient = new HttpClient();

        // Default config path in application data directory
        _configFilePath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MAria2",
            "UpdateSources",
            "plugins.json"
        );

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath));
    }

    public async Task<List<UpdateSourcePluginConfiguration>> GetConfiguredPluginsAsync()
    {
        try 
        {
            if (!File.Exists(_configFilePath))
            {
                // Return default configurations if no file exists
                return GetDefaultPluginConfigurations();
            }

            var jsonContent = await File.ReadAllTextAsync(_configFilePath);
            var configurations = JsonSerializer.Deserialize<List<UpdateSourcePluginConfiguration>>(
                jsonContent, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new List<UpdateSourcePluginConfiguration>();

            // Decrypt sensitive information
            foreach (var config in configurations)
            {
                DecryptAuthenticationCredentials(config);
            }

            return configurations;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to load update source configurations: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Configuration Error", 
                "Could not load update source configurations."
            );
            return GetDefaultPluginConfigurations();
        }
    }

    public async Task<UpdateSourcePluginConfiguration> AddPluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration)
    {
        try 
        {
            // Validate configuration
            if (!await ValidatePluginConfigurationAsync(configuration))
            {
                throw new InvalidOperationException("Invalid plugin configuration");
            }

            // Encrypt sensitive credentials
            EncryptAuthenticationCredentials(configuration);

            var existingConfigurations = await GetConfiguredPluginsAsync();
            
            // Ensure unique ID
            if (existingConfigurations.Any(c => c.Id == configuration.Id))
            {
                configuration.Id = Guid.NewGuid().ToString();
            }

            existingConfigurations.Add(configuration);

            // Save updated configurations
            await SaveConfigurationsAsync(existingConfigurations);

            return configuration;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to add update source configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Configuration Error", 
                "Could not add update source configuration."
            );
            throw;
        }
    }

    public async Task UpdatePluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration)
    {
        try 
        {
            // Validate configuration
            if (!await ValidatePluginConfigurationAsync(configuration))
            {
                throw new InvalidOperationException("Invalid plugin configuration");
            }

            // Encrypt sensitive credentials
            EncryptAuthenticationCredentials(configuration);

            var existingConfigurations = await GetConfiguredPluginsAsync();
            
            // Find and replace the existing configuration
            var existingConfig = existingConfigurations
                .FirstOrDefault(c => c.Id == configuration.Id);

            if (existingConfig == null)
            {
                throw new KeyNotFoundException("Update source configuration not found");
            }

            existingConfigurations.Remove(existingConfig);
            existingConfigurations.Add(configuration);

            // Save updated configurations
            await SaveConfigurationsAsync(existingConfigurations);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to update update source configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Configuration Error", 
                "Could not update update source configuration."
            );
            throw;
        }
    }

    public async Task RemovePluginConfigurationAsync(string pluginId)
    {
        try 
        {
            var existingConfigurations = await GetConfiguredPluginsAsync();
            
            var configToRemove = existingConfigurations
                .FirstOrDefault(c => c.Id == pluginId);

            if (configToRemove != null)
            {
                existingConfigurations.Remove(configToRemove);
                await SaveConfigurationsAsync(existingConfigurations);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to remove update source configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Configuration Error", 
                "Could not remove update source configuration."
            );
            throw;
        }
    }

    public async Task<bool> ValidatePluginConfigurationAsync(
        UpdateSourcePluginConfiguration configuration)
    {
        try 
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(configuration.Name))
            {
                return false;
            }

            // Validate URL if applicable
            if (!string.IsNullOrWhiteSpace(configuration.BaseUrl) && 
                !Uri.TryCreate(configuration.BaseUrl, UriKind.Absolute, out _))
            {
                return false;
            }

            // Test connection if possible
            return await TestUpdateSourceConnectionAsync(configuration);
        }
        catch 
        {
            return false;
        }
    }

    public async Task<bool> TestUpdateSourceConnectionAsync(
        UpdateSourcePluginConfiguration configuration)
    {
        try 
        {
            // Basic connection test for different plugin types
            switch (configuration.PluginType?.ToLowerInvariant())
            {
                case "github":
                    return await TestGitHubConnectionAsync(configuration);
                
                case "gitlab":
                    return await TestGitLabConnectionAsync(configuration);
                
                default:
                    // Generic URL test for custom repositories
                    if (!string.IsNullOrWhiteSpace(configuration.BaseUrl))
                    {
                        var response = await _httpClient.GetAsync(configuration.BaseUrl);
                        return response.IsSuccessStatusCode;
                    }
                    return true;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Connection test failed: {ex.Message}");
            return false;
        }
    }

    private async Task SaveConfigurationsAsync(
        List<UpdateSourcePluginConfiguration> configurations)
    {
        var jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true 
        };

        var jsonContent = JsonSerializer.Serialize(configurations, jsonOptions);
        await File.WriteAllTextAsync(_configFilePath, jsonContent);
    }

    private void EncryptAuthenticationCredentials(
        UpdateSourcePluginConfiguration configuration)
    {
        if (configuration.Authentication == null) return;

        try 
        {
            // Only encrypt if username and secret are provided
            if (!string.IsNullOrWhiteSpace(configuration.Authentication.Username) &&
                !string.IsNullOrWhiteSpace(configuration.Authentication.Secret))
            {
                var credentialsToEncrypt = 
                    $"{configuration.Authentication.Username}:{configuration.Authentication.Secret}";

                configuration.Authentication.EncryptedCredentials = 
                    EncryptString(credentialsToEncrypt);

                // Clear original credentials
                configuration.Authentication.Username = null;
                configuration.Authentication.Secret = null;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Credential encryption failed: {ex.Message}");
        }
    }

    private void DecryptAuthenticationCredentials(
        UpdateSourcePluginConfiguration configuration)
    {
        if (configuration.Authentication == null) return;

        try 
        {
            if (!string.IsNullOrWhiteSpace(configuration.Authentication.EncryptedCredentials))
            {
                var decryptedCredentials = 
                    DecryptString(configuration.Authentication.EncryptedCredentials);

                var parts = decryptedCredentials.Split(':');
                if (parts.Length == 2)
                {
                    configuration.Authentication.Username = parts[0];
                    configuration.Authentication.Secret = parts[1];
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Credential decryption failed: {ex.Message}");
        }
    }

    private string EncryptString(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.IV = EncryptionIV;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    private string DecryptString(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.IV = EncryptionIV;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
        
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }

    private async Task<bool> TestGitHubConnectionAsync(
        UpdateSourcePluginConfiguration configuration)
    {
        try 
        {
            // GitHub API endpoint test
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                "https://api.github.com/rate_limit"
            );

            // Add authentication if credentials exist
            if (!string.IsNullOrWhiteSpace(configuration.Authentication?.Secret))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Token", 
                    configuration.Authentication.Secret
                );
            }

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch 
        {
            return false;
        }
    }

    private async Task<bool> TestGitLabConnectionAsync(
        UpdateSourcePluginConfiguration configuration)
    {
        try 
        {
            // GitLab API endpoint test
            var baseUrl = configuration.BaseUrl ?? "https://gitlab.com/api/v4";
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                $"{baseUrl}/version"
            );

            // Add authentication if credentials exist
            if (!string.IsNullOrWhiteSpace(configuration.Authentication?.Secret))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer", 
                    configuration.Authentication.Secret
                );
            }

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch 
        {
            return false;
        }
    }

    private List<UpdateSourcePluginConfiguration> GetDefaultPluginConfigurations()
    {
        return new List<UpdateSourcePluginConfiguration>
        {
            new UpdateSourcePluginConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "GitHub Official",
                PluginType = "GitHub",
                BaseUrl = "https://api.github.com",
                SupportedDependencies = new List<string> 
                { 
                    "Aria2", 
                    "YtDlp", 
                    "Wget" 
                },
                Priority = 10,
                IsEnabled = true,
                SupportedVersions = new Dictionary<string, string>
                {
                    { "Aria2", "1.35.0-1.40.0" },
                    { "YtDlp", "2023.01.0-2024.01.0" }
                }
            },
            new UpdateSourcePluginConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "MAria2 Custom Repository",
                PluginType = "CustomRepository",
                BaseUrl = "https://updates.maria2.org/dependencies",
                SupportedDependencies = new List<string> 
                { 
                    "MAria2", 
                    "CustomEngines" 
                },
                Priority = 50,
                IsEnabled = true,
                SupportedVersions = new Dictionary<string, string>
                {
                    { "MAria2", "1.0.0-2.0.0" }
                }
            }
        };
    }
}
