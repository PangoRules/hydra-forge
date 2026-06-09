using HydraForge.Application.Cards;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Cards;

public class EfCardWatcherRepository(HydraForgeDbContext context) : ICardWatcherRepository
{
    public async Task<CardWatcher?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        return await context.CardWatchers
            .FirstOrDefaultAsync(w => w.CardId == cardId && w.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<CardWatcher>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardWatchers
            .Where(w => w.CardId == cardId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CardWatcher watcher, CancellationToken ct = default)
    {
        context.CardWatchers.Add(watcher);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        var watcher = await context.CardWatchers
            .FirstOrDefaultAsync(w => w.CardId == cardId && w.UserId == userId, ct);
        if (watcher != null)
            context.CardWatchers.Remove(watcher);
        await context.SaveChangesAsync(ct);
    }
}