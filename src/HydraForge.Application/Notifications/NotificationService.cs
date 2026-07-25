using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public class NotificationService(INotificationRepository notifRepo, INotificationHubBus hubBus) : INotificationService
{
    private readonly INotificationRepository _notifRepo = notifRepo;
    private readonly INotificationHubBus _hubBus = hubBus;

    public async Task NotifyAsync(NotifyRequest request, CancellationToken ct = default)
    {
        if (request.UserId == request.ActorId)
            return;

        var notif = Notification.Create(
            request.UserId,
            request.Title,
            request.Body,
            request.Message ?? request.Title,
            request.CardId,
            request.ProjectId,
            request.ActionUrl
        );

        await _notifRepo.AddAsync(notif, ct);
        await _hubBus.SendNotificationAsync(request.UserId, notif, ct);
    }
}
