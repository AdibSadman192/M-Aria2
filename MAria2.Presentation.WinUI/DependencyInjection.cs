using Microsoft.Extensions.DependencyInjection;
using MAria2.Presentation.WinUI.Services;

namespace MAria2.Presentation.WinUI;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentationServices(
        this IServiceCollection services, 
        Action<SettingsService.AppSettings> configureSettings = null)
    {
        // Register presentation layer services
        services.AddSingleton<ErrorHandlingService>();
        services.AddSingleton<NotificationService>();
        
        // Settings service with optional configuration
        var settingsService = new SettingsService();
        var appSettings = settingsService.GetAppSettings();
        
        // Apply custom configuration if provided
        configureSettings?.Invoke(appSettings);
        settingsService.SaveAppSettings(appSettings);
        
        services.AddSingleton(settingsService);
        services.AddSingleton(appSettings);

        return services;
    }
}
