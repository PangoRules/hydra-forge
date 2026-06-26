using HydraForge.Application.Cards;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Cards;

public class EfCardRepository(HydraForgeDbContext context) : ICardRepository
{
    public async Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.Cards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
    {
        return await context.Cards.Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
    }

    public async Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
    {
        return await context.Cards
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.CardNumber == cardNumber && c.ArchivedAt == null, ct);
    }

    public async Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
    {
        var query = context.Cards.Where(c => c.ProjectId == projectId);

        if (filter.ColumnId.HasValue)
            query = query.Where(c => c.ColumnId == filter.ColumnId.Value);

        if (!filter.IncludeArchived)
            query = query.Where(c => c.ArchivedAt == null);

        if (filter.Type.HasValue)
            query = query.Where(c => c.Type == filter.Type.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(c => EF.Functions.ILike(c.Title, $"%{filter.Search}%"));

        if (filter.ArchivedLimit.HasValue)
            query = query.OrderByDescending(c => c.ArchivedAt ?? c.CreatedAt).Take(filter.ArchivedLimit.Value);

        return await query.OrderBy(c => c.Position).ToListAsync(ct);
    }

    public async Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
    {
        var max = await context.Cards
            .Where(c => c.ProjectId == projectId && c.ArchivedAt == null)
            .MaxAsync(c => (int?)c.CardNumber, ct);
        return max ?? 0;
    }

    public async Task AddAsync(Card card, CancellationToken ct = default)
    {
        context.Cards.Add(card);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        context.Cards.Update(card);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        context.Cards.UpdateRange(cards);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid cardId, CancellationToken ct = default)
    {
        var card = await context.Cards.FindAsync([cardId], ct);
        if (card != null)
            context.Cards.Remove(card);
        await context.SaveChangesAsync(ct);
    }

    public async Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default)
    {
        var cards = await context.Cards
            .Where(c => c.ColumnId == columnId && c.Position > exceptPosition && c.ArchivedAt == null)
            .ToListAsync(ct);

        foreach (var card in cards)
            card.Position -= 1;

        await context.SaveChangesAsync(ct);
    }

    public async Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
    {
        return await context.Cards.CountAsync(c => c.ColumnId == columnId && c.ArchivedAt == null, ct);
    }
}