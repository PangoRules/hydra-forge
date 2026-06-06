namespace HydraForge.Application.Projects;

public interface IChatArchiveService
{
    Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default);
    Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default);
}