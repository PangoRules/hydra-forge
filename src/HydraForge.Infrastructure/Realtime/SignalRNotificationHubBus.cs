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
}
