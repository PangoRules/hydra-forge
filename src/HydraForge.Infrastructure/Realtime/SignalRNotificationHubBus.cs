using HydraForge.Application.Notifications;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Entities.PersonalSpace;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

public class SignalRNotificationHubBus(IHubContext<NotificationHub, INotificationHub> hubContext) : INotificationHubBus
{
    public async Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct)
    {
        await hubContext.Clients.Group($"user-{userId}").OnNotificationReceived(
            new NotificationReceivedEvent(
                notification.Id,
                notification.Title,
                notification.Body,
                notification.CardId,
                notification.ProjectId,
                notification.ActionUrl,
                notification.CreatedAt,
                notification.IsRead
            )
        );
    }

    // Each recipient's Notification has its own id (their own row, needed for their own
    // mark-as-read) — the payload isn't shareable across a SignalR group even when the
    // display text matches, so this is still N SendAsync calls. Kept here rather than in
    // NotificationService so the caller only ever sees one method call.
    public async Task SendNotificationsBatchAsync(IReadOnlyList<(Guid UserId, Notification Notification)> items, CancellationToken ct = default)
    {
        foreach (var (userId, notification) in items)
            await SendNotificationAsync(userId, notification, ct);
    }
}
