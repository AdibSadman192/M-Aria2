using Microsoft.Extensions.DependencyInjection;
using MAria2.Core.Interfaces;
using MAria2.Infrastructure.Engines;
using MAria2.Core.Enums;

namespace MAria2.Infrastructure.DependencyInjection;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddDownloadEngines(this IServiceCollection services)
    {
        // Register existing download engines
        services.AddSingleton<IDownloadEngine, Aria2DownloadEngine>(
            sp => new Aria2DownloadEngine(
                sp.GetRequiredService<ILoggingService>(),
                sp.GetRequiredService<ConfigurationManager>()
            )
        );

        services.AddSingleton<IDownloadEngine, YtDlpDownloadEngine>(
            sp => new YtDlpDownloadEngine(
                sp.GetRequiredService<ILoggingService>(),
                sp.GetRequiredService<ConfigurationManager>()
            )
        );

        // Register new WinInet download engine
        services.AddSingleton<IDownloadEngine, WinInetDownloadEngine>(
            sp => new WinInetDownloadEngine(
                sp.GetRequiredService<ILoggingService>(),
                sp.GetRequiredService<ConfigurationManager>()
            )
        );

        // Register engine selection service
        services.AddSingleton<EngineSelectionService>(
            sp => new EngineSelectionService(
                sp.GetServices<IDownloadEngine>().ToList(),
                sp.GetRequiredService<ILoggingService>()
            )
        );

        return services;
    }
}
