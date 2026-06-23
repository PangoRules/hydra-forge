using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.Tests;

internal class FakeProjectBoardEventPublisher : IProjectBoardEventPublisher
{
    public List<ProjectBoardEventEnvelope> PublishedEvents { get; } = [];

    public Task PublishAsync(ProjectBoardEventEnvelope envelope, CancellationToken ct = default)
    {
        PublishedEvents.Add(envelope);
        return Task.CompletedTask;
    }
}

internal class NullSnapshotRefresher : IProjectSnapshotRefresher
{
    public Task RefreshAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<ProjectContextSnapshot?> GetSnapshotAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult<ProjectContextSnapshot?>(null);
}
