using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.ProjectDocuments;

public interface IProjectDocumentRepository
{
    Task<ProjectDocument?> GetByIdAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDocument>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectDocument?> GetByDocTypeAsync(Guid projectId, ProjectDocType docType, CancellationToken ct = default);
    Task<ProjectDocumentVersion?> GetVersionAsync(Guid documentId, int version, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDocumentVersion>> ListVersionsAsync(Guid documentId, CancellationToken ct = default);
    Task AddAsync(ProjectDocument document, CancellationToken ct = default);
    Task AddVersionAsync(ProjectDocumentVersion version, CancellationToken ct = default);
    Task UpdateAsync(ProjectDocument document, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
