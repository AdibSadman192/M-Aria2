using Microsoft.Extensions.DependencyInjection;
using Moq;
using MAria2.Core.Interfaces;
using MAria2.Core.Configuration;
using MAria2.Application.Services;
using MAria2.Infrastructure.Engines;

namespace MAria2.IntegrationTests.TestConfiguration;

public static class TestDependencyInjection
{
    public static ServiceProvider CreateTestServiceProvider()
    {
        var services = new ServiceCollection();

        // Core Services
        services.AddSingleton<ILoggingService>(new Mock<ILoggingService>().Object);
        services.AddSingleton<ConfigurationManager>(sp => 
            new ConfigurationManager(sp.GetRequiredService<ILoggingService>()));

        // Download Services
        services.AddTransient<IDownloadQueueService, DownloadQueueService>();
        services.AddTransient<EngineSelectionService>();
        services.AddTransient<SplitDownloadManager>();

        // Notification Services
        services.AddTransient<INotificationService, NotificationService>();
        services.AddTransient<NotificationService.NotificationRouter>();

        // Thumbnail and Playlist Services
        services.AddTransient<IThumbnailExtractionService, ThumbnailExtractionService>();
        services.AddTransient<IPlaylistManagementService, PlaylistManagementService>();

        // Download Engines
        services.AddTransient<IDownloadEngine, Aria2DownloadEngine>();
        services.AddTransient<IDownloadEngine, YtDlpDownloadEngine>();

        // Verification Services
        services.AddTransient<IDownloadVerificationService, DownloadVerificationService>();

        return services.BuildServiceProvider();
    }
}
