using HydraForge.Application.Cards;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Cards;

public class EfCardAssigneeRepository(HydraForgeDbContext context) : ICardAssigneeRepository
{
    public async Task<CardAssignee?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        return await context.CardAssignees
            .FirstOrDefaultAsync(a => a.CardId == cardId && a.UserId == userId, ct);
    }

    public async Task<ILookup<Guid, CardAssignee>> ListByCardIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
    {
        var result = await context.CardAssignees
            .Where(a => cardIds.Contains(a.CardId))
            .ToListAsync(ct);
        return result.ToLookup(a => a.CardId);
    }

    public async Task<IReadOnlyList<CardAssignee>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardAssignees
            .Where(a => a.CardId == cardId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CardAssignee assignee, CancellationToken ct = default)
    {
        context.CardAssignees.Add(assignee);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        var assignee = await context.CardAssignees
            .FirstOrDefaultAsync(a => a.CardId == cardId && a.UserId == userId, ct);
        if (assignee != null)
            context.CardAssignees.Remove(assignee);
        await context.SaveChangesAsync(ct);
    }
}