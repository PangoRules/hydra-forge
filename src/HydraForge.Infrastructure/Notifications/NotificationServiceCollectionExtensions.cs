using HydraForge.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        services.Configure<NtfyOptions>(_ => { });

        // Named HttpClient for NtfyClient — no typed client factory, bypasses
        // DefaultTypedHttpClientFactory which can't resolve string? ctor params.
        services.AddHttpClient(nameof(NtfyClient));
        services.AddTransient<INtfyClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(NtfyClient));
            return new NtfyClient(httpClient, sp.GetRequiredService<IOptions<NtfyOptions>>());
        });

        return services;
    }
}
