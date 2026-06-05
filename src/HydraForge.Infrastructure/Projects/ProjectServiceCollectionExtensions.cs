using HydraForge.Application.Projects;
using HydraForge.Infrastructure.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Projects;

public static class ProjectServiceCollectionExtensions
{
    public static IServiceCollection AddProjectServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<IColumnRepository, EfColumnRepository>();
        services.AddScoped<IProjectMemberRepository, EfProjectMemberRepository>();
        services.AddScoped<IProjectContextSnapshotRepository, EfProjectContextSnapshotRepository>();
        services.AddScoped<IChatArchiveService, EfChatArchiveService>();
        services.AddScoped<ProjectService>();

        return services;
    }
}