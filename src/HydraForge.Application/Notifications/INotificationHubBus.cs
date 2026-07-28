using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public interface INotificationHubBus
{
    Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct = default);

    // Each recipient has their own Notification row (own id, needed for their own
    // mark-as-read), so even when the display text is identical across a batch, the
    // payload isn't shareable across users — this is still N SendAsync calls under the
    // hood. The batch method just keeps that fan-out encapsulated here instead of
    // inline in NotificationService, same as AddRangeAsync hides the repository's own
    // persistence mechanics.
    Task SendNotificationsBatchAsync(IReadOnlyList<(Guid UserId, Notification Notification)> items, CancellationToken ct = default);
}
