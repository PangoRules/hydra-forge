namespace HydraForge.Application.Notifications;

public interface INtfyClient
{
    Task PublishAsync(Guid userId, string title, string? body, CancellationToken ct = default);
}
