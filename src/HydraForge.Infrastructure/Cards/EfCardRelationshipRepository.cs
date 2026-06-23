using HydraForge.Application.Cards;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Cards;

public class EfCardRelationshipRepository(HydraForgeDbContext context) : ICardRelationshipRepository
{
    public async Task<IReadOnlyList<CardRelationship>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var cardIds = await context.Cards
            .Where(c => c.ProjectId == projectId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        return await context.CardRelationships
            .Where(r => cardIds.Contains(r.SourceCardId) || cardIds.Contains(r.TargetCardId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardRelationship>> ListActiveByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var cardIds = await context.Cards
            .Where(c => c.ProjectId == projectId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        return await context.CardRelationships
            .Where(r => r.ArchivedAt == null && (cardIds.Contains(r.SourceCardId) || cardIds.Contains(r.TargetCardId)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardRelationship>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardRelationships
            .Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardRelationships
            .Where(r => r.TargetCardId == cardId && r.Type == RelationshipType.BlockedBy && r.ArchivedAt == null)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardRelationships
            .Where(r => r.SourceCardId == cardId && r.Type == RelationshipType.Precedes && r.ArchivedAt == null)
            .ToListAsync(ct);
    }

    public async Task<CardRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.CardRelationships.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<CardRelationship>> ListActiveByCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardRelationships
            .Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null)
            .ToListAsync(ct);
    }

    public async Task<CardRelationship?> FindActiveAsync(Guid sourceCardId, Guid targetCardId, RelationshipType type, CancellationToken ct = default)
    {
        return await context.CardRelationships
            .FirstOrDefaultAsync(r => r.SourceCardId == sourceCardId && r.TargetCardId == targetCardId && r.Type == type && r.ArchivedAt == null, ct);
    }

    public async Task AddAsync(CardRelationship relationship, CancellationToken ct = default)
    {
        context.CardRelationships.Add(relationship);
        await Task.CompletedTask;
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var rel = await context.CardRelationships.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rel != null)
            rel.ArchivedAt = DateTime.UtcNow;
    }

    public async Task ArchiveRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var existing = await context.CardRelationships
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
        foreach (var rel in existing)
            rel.ArchivedAt = DateTime.UtcNow;
    }
}
