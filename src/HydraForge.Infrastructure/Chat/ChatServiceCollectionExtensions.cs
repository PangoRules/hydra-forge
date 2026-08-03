namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

public static class ChatServiceCollectionExtensions
{
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IChatSessionRepository, EfChatSessionRepository>();
        services.AddScoped<ICardChatLinkRepository, EfCardChatLinkRepository>();
        services.AddScoped<IChatMessageRepository, EfChatMessageRepository>();
        services.AddScoped<IChatSessionDocumentRepository, EfChatSessionDocumentRepository>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IAgentPersonalityRepository, EfAgentPersonalityRepository>();
        services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();

        // Services
        services.AddScoped<IChatSessionService, ChatSessionService>();
        services.AddScoped<IChatRagRetriever, ChatRagRetriever>();

        // Background queue
        services.AddSingleton<IBackgroundTaskQueue, HangfireBackgroundTaskQueue>();

        // Background jobs
        services.AddScoped<HydraForge.Application.Chat.CloseSessionJob>();

        return services;
    }
}
