using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifRepo;
    private readonly INotificationHubBus _hubBus;
    private readonly INtfyClient? _ntfyClient;

    public NotificationService(
        INotificationRepository notifRepo,
        INotificationHubBus hubBus,
        INtfyClient? ntfyClient = null)
    {
        _notifRepo = notifRepo;
        _hubBus = hubBus;
        _ntfyClient = ntfyClient;
    }

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

        if (_ntfyClient != null)
            await _ntfyClient.PublishAsync(request.UserId, request.Title, request.Body, ct);

        await _hubBus.SendNotificationAsync(request.UserId, notif, ct);
    }
}