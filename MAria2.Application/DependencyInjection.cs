using Microsoft.Extensions.DependencyInjection;
using MAria2.Application.Services;

namespace MAria2.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services, 
        Action<DownloadQueueServiceOptions>? configureDownloadQueue = null)
    {
        // Configure download queue options
        var downloadQueueOptions = new DownloadQueueServiceOptions();
        configureDownloadQueue?.Invoke(downloadQueueOptions);

        // Register application services
        services.AddTransient<DownloadService>();
        services.AddTransient<EngineManagerService>();
        
        services.AddSingleton(sp => 
        {
            var downloadService = sp.GetRequiredService<DownloadService>();
            return new DownloadQueueService(
                downloadService, 
                downloadQueueOptions.MaxConcurrentDownloads
            );
        });

        return services;
    }

    public class DownloadQueueServiceOptions
    {
        public int MaxConcurrentDownloads { get; set; } = 5;
    }
}
