using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListByUserAsync(Guid userId, int skip, int take, bool? unreadOnly = null, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
