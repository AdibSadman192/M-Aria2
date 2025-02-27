using Microsoft.Extensions.DependencyInjection;
using MAria2.Core.Interfaces;
using MAria2.Infrastructure.Engines.Aria2;
using MAria2.Infrastructure.Repositories;

namespace MAria2.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        Action<Aria2Configuration>? configureAria2 = null,
        string databasePath = null)
    {
        // Configure Aria2 with default or custom settings
        var aria2Config = new Aria2Configuration();
        configureAria2?.Invoke(aria2Config);

        services.AddSingleton(aria2Config);
        services.AddTransient<Aria2ConnectionManager>();
        services.AddTransient<IDownloadEngine, Aria2Engine>();
        services.AddTransient<IEngineCapabilityProvider, Aria2CapabilityProvider>();

        // Register SQLite repository with optional database path
        services.AddSingleton<IDownloadRepository>(sp => 
            new SqliteDownloadRepository(databasePath));

        return services;
    }
}
