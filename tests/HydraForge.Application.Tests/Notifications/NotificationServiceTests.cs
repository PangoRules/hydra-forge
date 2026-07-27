using HydraForge.Application.Notifications;
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Tests.Notifications;

public class NotificationServiceTests
{
    internal class FakeNotificationRepository : INotificationRepository
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

    internal class FakeNotificationHubBus : INotificationHubBus
    {
        public List<(Guid UserId, Notification Notification)> Sent { get; } = [];

        public Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct = default)
        {
            Sent.Add((userId, notification));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task NotifyAsync_WhenUserIdEqualsActorId_DoesNotAddNotification()
    {
        var repo = new FakeNotificationRepository();
        var hubBus = new FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus);
        var userId = Guid.NewGuid();

        await service.NotifyAsync(
            new NotifyRequest(userId, userId, "Title", null, null, null, null, null)
        );

        Assert.Empty(repo.Added);
        Assert.Empty(hubBus.Sent);
    }

    [Fact]
    public async Task NotifyAsync_WhenDifferentUser_AddsNotification()
    {
        var repo = new FakeNotificationRepository();
        var hubBus = new FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus);
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await service.NotifyAsync(
            new NotifyRequest(userId, actorId, "Test Title", "Body", "Message", null, null, null)
        );

        Assert.Single(repo.Added);
        var notif = repo.Added[0];
        Assert.Equal(userId, notif.UserId);
        Assert.Equal("Test Title", notif.Title);
        Assert.Equal("Body", notif.Body);
        Assert.Equal("Message", notif.Message);
        Assert.False(notif.IsRead);
    }

    [Fact]
    public async Task NotifyAsync_WhenMessageIsNull_UsesTitleAsMessage()
    {
        var repo = new FakeNotificationRepository();
        var hubBus = new FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus);

        await service.NotifyAsync(
            new NotifyRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", null, null, null, null, null)
        );

        Assert.Equal("Title", repo.Added[0].Message);
    }

    [Fact]
    public async Task NotifyAsync_WhenDifferentUser_SendsToHubBus()
    {
        var repo = new FakeNotificationRepository();
        var hubBus = new FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus);
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await service.NotifyAsync(
            new NotifyRequest(userId, actorId, "Title", null, null, null, null, null)
        );

        Assert.Single(hubBus.Sent);
        Assert.Equal(userId, hubBus.Sent[0].UserId);
        Assert.Equal("Title", hubBus.Sent[0].Notification.Title);
    }

    [Fact]
    public async Task NotifyAsync_WhenUserIdEqualsActorId_DoesNotSendToHubBus()
    {
        var repo = new FakeNotificationRepository();
        var hubBus = new FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus);
        var userId = Guid.NewGuid();

        await service.NotifyAsync(
            new NotifyRequest(userId, userId, "Title", null, null, null, null, null)
        );

        Assert.Empty(hubBus.Sent);
    }
}
