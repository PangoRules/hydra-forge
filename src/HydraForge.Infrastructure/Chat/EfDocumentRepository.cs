namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfDocumentRepository(HydraForgeDbContext context) : IDocumentRepository
{
    public async Task<Document?> GetByIdAsync(Guid documentId, CancellationToken ct = default)
    {
        return await context.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
    }

    public async Task<IReadOnlyList<Document>> ListByUserAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await context
            .Documents.Where(d => d.UserId == userId && d.ArchivedAt == null)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        context.Documents.Add(document);
        await context.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid documentId, CancellationToken ct = default)
    {
        var doc = await context.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc != null)
        {
            doc.ArchivedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }
    }
}
