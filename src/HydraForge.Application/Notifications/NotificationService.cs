using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public class NotificationService(INotificationRepository notifRepo) : INotificationService
{
    private readonly INotificationRepository _notifRepo = notifRepo;

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
    }
}
