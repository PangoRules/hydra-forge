using HydraForge.Application.Checklist;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Checklist;

public class EfChecklistItemRepository(HydraForgeDbContext context) : IChecklistItemRepository
{
    public async Task<ChecklistItem?> GetByIdAsync(Guid itemId, CancellationToken ct = default)
    {
        return await context.ChecklistItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
    }

    public async Task<IReadOnlyList<ChecklistItem>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.ChecklistItems
            .Where(i => i.CardId == cardId)
            .OrderBy(i => i.Position)
            .ToListAsync(ct);
    }

    public async Task<int> GetMaxPositionAsync(Guid cardId, CancellationToken ct = default)
    {
        var max = await context.ChecklistItems
            .Where(i => i.CardId == cardId)
            .MaxAsync(i => (int?)i.Position, ct);
        return max ?? -1;
    }

    public async Task AddAsync(ChecklistItem item, CancellationToken ct = default)
    {
        context.ChecklistItems.Add(item);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ChecklistItem item, CancellationToken ct = default)
    {
        context.ChecklistItems.Update(item);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid itemId, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var item = await context.ChecklistItems.FindAsync([itemId], ct);
            if (item != null)
                context.ChecklistItems.Remove(item);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task CompactPositionsAsync(Guid cardId, int deletedPosition, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var toCompact = await context.ChecklistItems
                .Where(i => i.CardId == cardId && i.Position > deletedPosition)
                .ToListAsync(ct);

            foreach (var item in toCompact)
                item.Position -= 1;

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UpdatePositionsAsync(IReadOnlyList<ChecklistItem> items, CancellationToken ct = default)
    {
        context.ChecklistItems.UpdateRange(items);
        await context.SaveChangesAsync(ct);
    }
}