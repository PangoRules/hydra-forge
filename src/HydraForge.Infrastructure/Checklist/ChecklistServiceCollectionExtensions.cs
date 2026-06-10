using HydraForge.Application.Checklist;
using HydraForge.Infrastructure.Checklist;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Checklist;

public static class ChecklistServiceCollectionExtensions
{
    public static IServiceCollection AddChecklistServices(this IServiceCollection services)
    {
        services.AddScoped<IChecklistItemRepository, EfChecklistItemRepository>();
        services.AddScoped<ChecklistService>();
        return services;
    }
}