using Microsoft.Extensions.DependencyInjection;
using MAria2.Core.Interfaces;
using MAria2.Application.Services;

namespace MAria2.Infrastructure.DependencyInjection;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Media Information Service
        services.AddSingleton<IMediaInformationService, MediaInformationService>();

        // Register Channel Subscription Service
        services.AddSingleton<IChannelSubscriptionService, ChannelSubscriptionService>();

        // Register Post-Processing Service
        services.AddSingleton<IPostProcessingService, PostProcessingService>();

        return services;
    }
}
