using HydraForge.Application.Notifications;
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Tests.Notifications;

public class NotificationServiceTests
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

    [Fact]
    public async Task NotifyAsync_WhenUserIdEqualsActorId_DoesNotAddNotification()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo);
        var userId = Guid.NewGuid();

        await service.NotifyAsync(
            new NotifyRequest(userId, userId, "Title", null, null, null, null, null)
        );

        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task NotifyAsync_WhenDifferentUser_AddsNotification()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo);
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
        var service = new NotificationService(repo);

        await service.NotifyAsync(
            new NotifyRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", null, null, null, null, null)
        );

        Assert.Equal("Title", repo.Added[0].Message);
    }
}
