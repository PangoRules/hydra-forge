using HydraForge.Application.Documents;
using HydraForge.Application.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Documents;

public static class DocumentsServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentServices(this IServiceCollection services)
    {
        services.AddScoped<IHtmlToMarkdownConverter, ReverseMarkdownConverter>();
        services.AddScoped<ISpecPlanHtmlBackfillRepository, EfSpecPlanHtmlBackfillRepository>();
        services.AddScoped<SpecPlanHtmlBackfillJob>();
        return services;
    }
}
