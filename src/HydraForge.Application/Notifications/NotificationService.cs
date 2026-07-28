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

    public async Task NotifyBatchAsync(IReadOnlyList<NotifyRequest> requests, CancellationToken ct = default)
    {
        var notifications = new List<Notification>(requests.Count);
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            if (request.UserId == request.ActorId)
                continue;
            notifications.Add(Notification.Create(
                request.UserId,
                request.Title,
                request.Body,
                request.Message ?? request.Title,
                request.CardId,
                request.ProjectId,
                request.ActionUrl
            ));
        }

        if (notifications.Count == 0)
            return;

        try
        {
            await _notifRepo.AddRangeAsync(notifications, ct);

            for (int i = 0; i < notifications.Count; i++)
            {
                var req = requests[i];
                if (_ntfyClient != null)
                    await _ntfyClient.PublishAsync(req.UserId, req.Title, req.Body, ct);
                await _hubBus.SendNotificationAsync(req.UserId, notifications[i], ct);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to send batch notifications: {ex.Message}");
        }
    }
}