using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Infrastructure.Cards;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Cards;

public static class CardServiceCollectionExtensions
{
    public static IServiceCollection AddCardServices(this IServiceCollection services)
    {
        services.AddScoped<ICardRepository, EfCardRepository>();
        services.AddScoped<ICardAssigneeRepository, EfCardAssigneeRepository>();
        services.AddScoped<ICardWatcherRepository, EfCardWatcherRepository>();
        services.AddScoped<ICardRelationshipRepository, EfCardRelationshipRepository>();
        services.AddScoped<CardService>();
        return services;
    }
}