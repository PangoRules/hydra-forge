using HydraForge.Application.Cards;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Projects;
using HydraForge.Infrastructure.Cards;
using HydraForge.Infrastructure.ProjectSnapshots;
using HydraForge.Infrastructure.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Projects;

public static class ProjectServiceCollectionExtensions
{
    public static IServiceCollection AddProjectServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<IColumnRepository, EfColumnRepository>();
        services.AddScoped<ICardRepository, EfCardRepository>();
        services.AddScoped<IProjectMemberRepository, EfProjectMemberRepository>();
        services.AddScoped<IProjectContextSnapshotRepository, EfProjectContextSnapshotRepository>();
        services.AddScoped<IChatArchiveService, EfChatArchiveService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectMemberService>();
        services.AddScoped<IProjectSnapshotRefresher, ProjectSnapshotRefresher>();

        return services;
    }
}