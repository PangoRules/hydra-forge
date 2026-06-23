using HydraForge.Application.Realtime;

namespace HydraForge.Server.Tests;

internal class FakeProjectBoardEventPublisher : IProjectBoardEventPublisher
{
    public List<ProjectBoardEventEnvelope> PublishedEvents { get; } = [];

    public Task PublishAsync(ProjectBoardEventEnvelope envelope, CancellationToken ct = default)
    {
        PublishedEvents.Add(envelope);
        return Task.CompletedTask;
    }
}
