using HydraForge.Application.Cards;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Cards;

public class EfCardRelationshipRepository(HydraForgeDbContext context) : ICardRelationshipRepository
{
    public async Task<IReadOnlyList<CardRelationship>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardRelationships
            .Where(r => r.SourceCardId == cardId || r.TargetCardId == cardId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardRelationships
            .Where(r => r.TargetCardId == cardId && r.Type == RelationshipType.BlockedBy)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardRelationships
            .Where(r => r.SourceCardId == cardId && r.Type == RelationshipType.Precedes)
            .ToListAsync(ct);
    }
}