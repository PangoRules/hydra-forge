using HydraForge.Application.Attachments;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Attachments;

public class EfAttachmentRepository(HydraForgeDbContext db) : IAttachmentRepository
{
    public async Task<Attachment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Attachment>> ListByCardAsync(
        Guid cardId,
        CancellationToken ct = default
    ) =>
        await db
            .Attachments.Where(a => a.CardId == cardId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public Task AddAsync(Attachment attachment, CancellationToken ct = default)
    {
        db.Attachments.Add(attachment);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (attachment != null)
            db.Attachments.Remove(attachment);
    }
}
