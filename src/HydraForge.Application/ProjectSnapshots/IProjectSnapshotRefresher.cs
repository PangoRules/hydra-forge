using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.ProjectSnapshots;

public interface IProjectSnapshotRefresher
{
    Task RefreshAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectContextSnapshot?> GetSnapshotAsync(Guid projectId, CancellationToken ct = default);
}