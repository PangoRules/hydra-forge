using HydraForge.Application.Comments;
using HydraForge.Infrastructure.Comments;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Comments;

public static class CommentServiceCollectionExtensions
{
    public static IServiceCollection AddCommentServices(this IServiceCollection services)
    {
        services.AddScoped<ICommentRepository, EfCommentRepository>();
        services.AddScoped<CommentService>();
        return services;
    }
}