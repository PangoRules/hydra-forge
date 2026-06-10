using HydraForge.Application.Comments;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Comments;

public class EfCommentRepository(HydraForgeDbContext context) : ICommentRepository
{
    public async Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken ct = default)
    {
        return await context.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct);
    }

    public async Task<IReadOnlyList<Comment>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.Comments
            .Where(c => c.CardId == cardId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Comment comment, CancellationToken ct = default)
    {
        context.Comments.Add(comment);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Comment comment, CancellationToken ct = default)
    {
        context.Comments.Update(comment);
        await context.SaveChangesAsync(ct);
    }
}