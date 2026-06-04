namespace HydraForge.Infrastructure.Audit;

using HydraForge.Application.Audit;
using HydraForge.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddAuditServices(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogWriter, EfAuditLogWriter>();
        services.AddScoped<AuditService>();

        return services;
    }
}