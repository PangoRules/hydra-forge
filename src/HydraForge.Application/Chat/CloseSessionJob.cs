namespace HydraForge.Application.Chat;

public sealed class CloseSessionJob(IChatSessionService sessionService)
{
    public async Task RunAsync(Guid sessionId, Guid actorId, CancellationToken ct)
    {
        await sessionService.CloseAsync(sessionId, actorId, ct);
    }
}
