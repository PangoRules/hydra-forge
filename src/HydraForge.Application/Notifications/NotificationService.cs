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

        // ntfy and SignalR genuinely can't be batched below this point — this loop is
        // the real bottom, not a missed optimization:
        //   - ntfy's publish API is one topic (= one user) per HTTP request. There's no
        //     multi-topic publish endpoint, so N recipients means N POSTs no matter what.
        //   - Each recipient's Notification is its own DB row with its own id (needed for
        //     that user's own mark-as-read), so even when the display text is identical
        //     across the batch, the SignalR payload isn't shareable across a group of
        //     users — sending one shared id to multiple recipients would let one user
        //     mark-as-read a row that belongs to someone else.
        // The batch win already happened above: one AddRangeAsync instead of N inserts.
        for (int i = 0; i < notifications.Count; i++)
        {
            var req = validRequests[i];
            if (_ntfyClient != null)
                await _ntfyClient.PublishAsync(req.UserId, req.Title, req.Body, ct);
            await _hubBus.SendNotificationAsync(req.UserId, notifications[i], ct);
        }
    }
}