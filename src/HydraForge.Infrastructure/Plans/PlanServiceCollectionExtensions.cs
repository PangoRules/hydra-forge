using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Application.Plans;
using HydraForge.Infrastructure.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Plans;

public static class PlanServiceCollectionExtensions
{
    public static IServiceCollection AddPlanServices(this IServiceCollection services)
    {
        services.AddScoped<IPlanRepository, EfPlanRepository>();
        services.AddScoped<PlanService>();
        return services;
    }
}
