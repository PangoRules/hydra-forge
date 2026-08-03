using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Application.Chat;

public sealed class CloseSessionJob(IServiceProvider serviceProvider)
{
    public async Task RunAsync(Guid sessionId, Guid actorId, CancellationToken ct)
    {
        var svc = serviceProvider.GetRequiredService<ChatSessionService>();
        await svc.CloseAsync(sessionId, actorId, ct);
    }
}
