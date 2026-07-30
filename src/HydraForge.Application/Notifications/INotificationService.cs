namespace HydraForge.Application.Notifications;

public interface INotificationService
{
    Task NotifyAsync(NotifyRequest request, CancellationToken ct = default);
    Task NotifyBatchAsync(IReadOnlyList<NotifyRequest> requests, CancellationToken ct = default);
}
