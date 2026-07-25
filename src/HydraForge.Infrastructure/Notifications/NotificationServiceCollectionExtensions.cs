using HydraForge.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
