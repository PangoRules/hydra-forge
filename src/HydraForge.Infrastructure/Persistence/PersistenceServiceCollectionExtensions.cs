namespace HydraForge.Infrastructure.Persistence;

using HydraForge.Application.Health;
using HydraForge.Infrastructure.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found.");

        services.AddDbContext<HydraForgeDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        services.AddScoped<IHealthProbe, ServerHealthProbe>();
        services.AddScoped<IHealthProbe, DatabaseHealthProbe>();
        services.AddScoped<IHealthProbe, LlmProviderHealthProbe>();

        return services;
    }
}
