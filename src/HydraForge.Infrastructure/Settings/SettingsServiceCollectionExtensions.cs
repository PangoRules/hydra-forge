using HydraForge.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Settings;

public static class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsServices(this IServiceCollection services)
    {
        services.AddScoped<ISettingsRepository, EfSettingsRepository>();
        services.AddScoped<ISettingsProvider, CachedSettingsProvider>();
        services.AddMemoryCache();
        return services;
    }
}
