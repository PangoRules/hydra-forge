using HydraForge.Application.Columns;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Columns;

public static class ColumnServiceCollectionExtensions
{
    public static IServiceCollection AddColumnServices(this IServiceCollection services)
    {
        services.AddScoped<ColumnService>();
        return services;
    }
}