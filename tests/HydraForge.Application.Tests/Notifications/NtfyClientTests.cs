using HydraForge.Application.Notifications;
using HydraForge.Domain.Entities.PersonalSpace;
using Xunit;

namespace HydraForge.Application.Tests.Notifications;

public class NtfyClientTests
{
    private class FakeNotificationRepository : INotificationRepository
    {
        public List<Notification> Added { get; } = [];

        public Task AddAsync(Notification notification, CancellationToken ct = default)
        {
            Added.Add(notification);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(
            IReadOnlyList<Notification> notifications,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyList<Notification>> ListByUserAsync(
            Guid userId,
            int skip,
            int take,
            bool? unreadOnly = null,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task MarkAsReadAsync(
            Guid notificationId,
            Guid userId,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private class FakeNotificationHubBus : INotificationHubBus
    {
        public List<(Guid UserId, Notification Notification)> Sent { get; } = [];

        public Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct = default)
        {
            Sent.Add((userId, notification));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task NotificationService_WithNullNtfyClient_DoesNotThrow()
    {
        var repo = new FakeNotificationRepository();
        var hubBus = new FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus, ntfyClient: null);

        await service.NotifyAsync(new NotifyRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "Body", null, null, null, null));

        Assert.Single(repo.Added);
    }
}