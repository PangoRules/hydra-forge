using HydraForge.Application.Specs;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Specs;

public static class SpecServiceCollectionExtensions
{
    public static IServiceCollection AddSpecServices(this IServiceCollection services)
    {
        services.AddScoped<ISpecRepository, EfSpecRepository>();
        services.AddScoped<SpecService>();
        return services;
    }
}
