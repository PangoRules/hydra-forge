using HydraForge.Application.Notifications;
using Xunit;

namespace HydraForge.Application.Tests.Notifications;

public class NotificationTriggerTests
{
    private class FakeNotificationService : INotificationService
    {
        public List<NotifyRequest> Calls { get; } = [];
        public Task NotifyAsync(NotifyRequest request, CancellationToken ct = default)
        {
            Calls.Add(request);
            return Task.CompletedTask;
        }
        public Task NotifyBatchAsync(IReadOnlyList<NotifyRequest> requests, CancellationToken ct = default)
        {
            Calls.AddRange(requests);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task NotifyAsync_SameUserAsActor_IsSkipped()
    {
        var repo = new NotificationServiceTests.FakeNotificationRepository();
        var hubBus = new NotificationServiceTests.FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus);

        var userId = Guid.NewGuid();
        await service.NotifyAsync(new NotifyRequest(userId, userId, "Title", null, null, null, null, null));

        Assert.Empty(repo.Added);
        Assert.Empty(hubBus.Sent);
    }

    [Fact]
    public void NotifyRequest_RecordsAllFields()
    {
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var request = new NotifyRequest(
            userId, actorId, "Title", "Body", "Message",
            cardId, projectId, "/action");

        Assert.Equal(userId, request.UserId);
        Assert.Equal(actorId, request.ActorId);
        Assert.Equal("Title", request.Title);
        Assert.Equal("Body", request.Body);
        Assert.Equal("Message", request.Message);
        Assert.Equal(cardId, request.CardId);
        Assert.Equal(projectId, request.ProjectId);
        Assert.Equal("/action", request.ActionUrl);
    }
}
