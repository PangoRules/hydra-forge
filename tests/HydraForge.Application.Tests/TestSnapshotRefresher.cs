using HydraForge.Application.ProjectSnapshots;
using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.Tests;

internal class NullSnapshotRefresher : IProjectSnapshotRefresher
{
    public Task RefreshAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<ProjectContextSnapshot?> GetSnapshotAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult<ProjectContextSnapshot?>(null);
}
