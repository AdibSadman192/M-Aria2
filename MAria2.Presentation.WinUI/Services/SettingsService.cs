using System.Text.Json;
using Windows.Storage;

namespace MAria2.Presentation.WinUI.Services;

public class SettingsService
{
    private readonly ApplicationDataContainer _localSettings;

    public SettingsService()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
    }

    public T GetSetting<T>(string key, T defaultValue = default)
    {
        if (_localSettings.Values.TryGetValue(key, out object value))
        {
            try
            {
                // If stored as JSON string, deserialize
                if (value is string jsonString)
                {
                    return JsonSerializer.Deserialize<T>(jsonString);
                }

                // Direct type conversion
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    public void SetSetting<T>(string key, T value)
    {
        // For complex types, serialize to JSON
        if (value is not string and not int and not bool)
        {
            _localSettings.Values[key] = JsonSerializer.Serialize(value);
        }
        else
        {
            _localSettings.Values[key] = value;
        }
    }

    // Application-specific settings
    public class AppSettings
    {
        public int MaxConcurrentDownloads { get; set; } = 5;
        public string DefaultDownloadPath { get; set; }
        public bool IsDarkModeEnabled { get; set; } = true;
        public string PreferredDownloadEngine { get; set; } = "Aria2";
        public bool AutoStartDownloads { get; set; } = true;
        public bool EnableSpeedLimit { get; set; } = false;
        public long MaxDownloadSpeed { get; set; } = 0; // Unlimited
        public long MaxUploadSpeed { get; set; } = 0; // Unlimited
    }

    public AppSettings GetAppSettings()
    {
        return GetSetting<AppSettings>(nameof(AppSettings)) ?? new AppSettings();
    }

    public void SaveAppSettings(AppSettings settings)
    {
        SetSetting(nameof(AppSettings), settings);
    }
}
