using System.Text.Json;
using MAria2.Core.Interfaces;
using MAria2.Core.Entities;

namespace MAria2.Application.Services;

public class ConfigurationService : IConfigurationService
{
    private const string CONFIG_FILE_PATH = "appsettings.json";
    private readonly ILoggingService _loggingService;

    public ConfigurationService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public EnginePreferences GetEnginePreferences()
    {
        try 
        {
            if (!File.Exists(CONFIG_FILE_PATH))
            {
                return CreateDefaultEnginePreferences();
            }

            var jsonContent = File.ReadAllText(CONFIG_FILE_PATH);
            var preferences = JsonSerializer.Deserialize<EnginePreferences>(jsonContent);
            
            return preferences ?? CreateDefaultEnginePreferences();
        }
        catch (Exception ex)
        {
            _loggingService.LogWarning($"Error reading configuration: {ex.Message}");
            return CreateDefaultEnginePreferences();
        }
    }

    public void SaveEnginePreferences(EnginePreferences preferences)
    {
        try 
        {
            var jsonContent = JsonSerializer.Serialize(preferences, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            File.WriteAllText(CONFIG_FILE_PATH, jsonContent);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error saving configuration: {ex.Message}");
        }
    }

    private EnginePreferences CreateDefaultEnginePreferences()
    {
        return new EnginePreferences 
        {
            PreferredEngine = "Aria2",
            GlobalDownloadSettings = new GlobalDownloadSettings 
            {
                MaxConcurrentDownloads = 5,
                DefaultDownloadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                    "Downloads"
                ),
                SpeedLimit = 0 // Unlimited
            },
            EngineSpecificRules = new Dictionary<string, EngineRule>
            {
                ["Torrent"] = new EngineRule { PreferredEngine = "Aria2" },
                ["YouTube"] = new EngineRule { PreferredEngine = "YtDlp" },
                ["HTTP"] = new EngineRule { PreferredEngine = "WinInet" }
            }
        };
    }
}

public class EnginePreferences
{
    public string PreferredEngine { get; set; }
    public GlobalDownloadSettings GlobalDownloadSettings { get; set; }
    public Dictionary<string, EngineRule> EngineSpecificRules { get; set; }
}

public class GlobalDownloadSettings
{
    public int MaxConcurrentDownloads { get; set; }
    public string DefaultDownloadPath { get; set; }
    public long SpeedLimit { get; set; }
}

public class EngineRule
{
    public string PreferredEngine { get; set; }
    public bool AllowEngineSwitch { get; set; } = true;
}
