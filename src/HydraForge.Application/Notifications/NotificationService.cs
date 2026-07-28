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
        // Kept in lockstep with `notifications` below — indexing back into the original
        // `requests` list here was a bug: skipping even one self-notification mid-list
        // shifts every later notifications[i] out of alignment with requests[i], so the
        // wrong title/body/recipient could get dispatched via ntfy/SignalR.
        var validRequests = new List<NotifyRequest>(requests.Count);
        var notifications = new List<Notification>(requests.Count);
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            if (request.UserId == request.ActorId)
                continue;
            validRequests.Add(request);
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

        // No try/catch here, deliberately — same as NotifyAsync above. The caller wraps
        // this in try/catch with IWarnLogger (see the Plan 7 notification-trigger
        // convention); a second catch-and-swallow layer here would just make that outer
        // one dead code.
        await _notifRepo.AddRangeAsync(notifications, ct);

        if (_ntfyClient != null)
        {
            var ntfyItems = validRequests
                .Select(req => (req.UserId, req.Title, req.Body))
                .ToList();
            await _ntfyClient.PublishBatchAsync(ntfyItems, ct);
        }

        var hubItems = validRequests
            .Select((req, i) => (req.UserId, notifications[i]))
            .ToList();
        await _hubBus.SendNotificationsBatchAsync(hubItems, ct);
    }
}