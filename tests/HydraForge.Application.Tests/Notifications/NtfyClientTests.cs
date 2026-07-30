using HydraForge.Application.Notifications;

namespace HydraForge.Application.Tests.Notifications;

public class NtfyClientTests
{
    [Fact]
    public async Task NotificationService_WithNullNtfyClient_DoesNotThrow()
    {
        var repo = new NotificationServiceTests.FakeNotificationRepository();
        var hubBus = new NotificationServiceTests.FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus, ntfyClient: null);

        await service.NotifyAsync(
            new NotifyRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Title",
                "Body",
                null,
                null,
                null,
                null
            )
        );

        Assert.Single(repo.Added);
    }
}
