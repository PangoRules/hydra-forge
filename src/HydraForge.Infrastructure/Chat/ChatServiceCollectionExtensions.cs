namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using Microsoft.Extensions.DependencyInjection;

public static class ChatServiceCollectionExtensions
{
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();
        services.AddScoped<IChatRagRetriever, ChatRagRetriever>();
        return services;
    }
}
