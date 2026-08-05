using HydraForge.Application.ProjectDocuments;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.ProjectDocuments;

public static class ProjectDocumentServiceCollectionExtensions
{
    public static IServiceCollection AddProjectDocumentServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectDocumentRepository, EfProjectDocumentRepository>();
        services.AddScoped<ProjectDocumentService>();
        return services;
    }
}
