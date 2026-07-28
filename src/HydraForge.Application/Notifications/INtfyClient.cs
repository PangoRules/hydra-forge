namespace HydraForge.Application.Notifications;

public interface INtfyClient
{
    Task PublishAsync(Guid userId, string title, string? body, CancellationToken ct = default);

    // ntfy's publish API is one topic (= one user) per HTTP request — there's no
    // multi-topic publish endpoint, so this is still N requests under the hood. The
    // point of a dedicated batch method is keeping that fan-out mechanic encapsulated
    // here instead of inline in NotificationService, same as AddRangeAsync hides
    // however the repository actually persists a range.
    Task PublishBatchAsync(IReadOnlyList<(Guid UserId, string Title, string? Body)> items, CancellationToken ct = default);
}
