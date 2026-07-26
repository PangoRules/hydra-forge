using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public interface INotificationHubBus
{
    Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct = default);
}
