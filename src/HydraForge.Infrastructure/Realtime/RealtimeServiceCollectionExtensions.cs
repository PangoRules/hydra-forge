using HydraForge.Application.Notifications;
using HydraForge.Application.Realtime;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Realtime;

public static class RealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddRealtimeServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectBoardEventPublisher, SignalRProjectBoardEventPublisher>();
        services.AddScoped<INotificationHubBus, SignalRNotificationHubBus>();
        return services;
    }
}
