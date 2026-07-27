using HydraForge.Application.Notifications;

namespace HydraForge.Server.Tests;

internal class FakeNotificationService : INotificationService
{
    public Task NotifyAsync(NotifyRequest request, CancellationToken ct = default)
        => Task.CompletedTask;
}