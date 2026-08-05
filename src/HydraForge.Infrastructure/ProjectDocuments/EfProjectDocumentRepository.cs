using HydraForge.Application.ProjectDocuments;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.ProjectDocuments;

public class EfProjectDocumentRepository(HydraForgeDbContext context) : IProjectDocumentRepository
{
    public async Task<ProjectDocument?> GetByIdAsync(Guid documentId, CancellationToken ct = default) =>
        await context.ProjectDocuments.FindAsync([documentId], ct);

    public async Task<IReadOnlyList<ProjectDocument>> ListByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await context.ProjectDocuments
            .Where(d => d.ProjectId == projectId && d.ArchivedAt == null)
            .OrderBy(d => d.DocType).ThenBy(d => d.Title)
            .ToListAsync(ct);

    public async Task<ProjectDocument?> GetByDocTypeAsync(Guid projectId, ProjectDocType docType, CancellationToken ct = default) =>
        await context.ProjectDocuments
            .FirstOrDefaultAsync(d => d.ProjectId == projectId && d.DocType == docType && d.ArchivedAt == null, ct);

    public async Task<ProjectDocumentVersion?> GetVersionAsync(Guid documentId, int version, CancellationToken ct = default) =>
        await context.ProjectDocumentVersions
            .FirstOrDefaultAsync(v => v.ProjectDocumentId == documentId && v.Version == version, ct);

    public async Task<IReadOnlyList<ProjectDocumentVersion>> ListVersionsAsync(Guid documentId, CancellationToken ct = default) =>
        await context.ProjectDocumentVersions
            .Where(v => v.ProjectDocumentId == documentId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);

    public async Task AddAsync(ProjectDocument document, CancellationToken ct = default) =>
        await context.ProjectDocuments.AddAsync(document, ct);

    public async Task AddVersionAsync(ProjectDocumentVersion version, CancellationToken ct = default) =>
        await context.ProjectDocumentVersions.AddAsync(version, ct);

    public Task UpdateAsync(ProjectDocument document, CancellationToken ct = default)
    {
        context.ProjectDocuments.Update(document);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await context.SaveChangesAsync(ct);
}