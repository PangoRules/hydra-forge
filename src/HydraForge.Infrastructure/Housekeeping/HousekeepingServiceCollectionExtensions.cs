using HydraForge.Application.Housekeeping;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Housekeeping;

public static class HousekeepingServiceCollectionExtensions
{
    public static IServiceCollection AddHousekeepingServices(this IServiceCollection services)
    {
        services.AddScoped<IHousekeepingRepository, EfHousekeepingRepository>();
        services.AddScoped<HousekeepingJob>();
        return services;
    }
}
