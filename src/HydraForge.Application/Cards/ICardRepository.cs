using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Cards;

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    );
    Task<Card?> GetByProjectAndNumberAsync(
        Guid projectId,
        int cardNumber,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<Card>> ListByProjectAsync(
        Guid projectId,
        CardListFilter filter,
        CancellationToken ct = default
    );
    Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Card card, CancellationToken ct = default);
    Task UpdateAsync(Card card, CancellationToken ct = default);
    Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default);
    Task DeleteAsync(Guid cardId, CancellationToken ct = default);
    Task CompactColumnPositionsAsync(
        Guid columnId,
        int exceptPosition,
        CancellationToken ct = default
    );
    Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default);
}

public interface ICardAssigneeRepository
{
    Task<CardAssignee?> GetByCardAndUserAsync(
        Guid cardId,
        Guid userId,
        CancellationToken ct = default
    );
    Task<ILookup<Guid, CardAssignee>> ListByCardIdsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<CardAssignee>> ListByCardAsync(Guid cardId, CancellationToken ct = default);
    Task AddAsync(CardAssignee assignee, CancellationToken ct = default);
    Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default);
}

public interface ICardWatcherRepository
{
    Task<CardWatcher?> GetByCardAndUserAsync(
        Guid cardId,
        Guid userId,
        CancellationToken ct = default
    );
    Task<ILookup<Guid, CardWatcher>> ListByCardIdsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<CardWatcher>> ListByCardAsync(Guid cardId, CancellationToken ct = default);
    Task AddAsync(CardWatcher watcher, CancellationToken ct = default);
    Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default);
}

public interface ICardRelationshipRepository
{
    Task<IReadOnlyList<CardRelationship>> ListByProjectAsync(
        Guid projectId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<CardRelationship>> ListActiveByProjectAsync(
        Guid projectId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<CardRelationship>> ListByCardAsync(
        Guid cardId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(
        Guid cardId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(
        Guid cardId,
        CancellationToken ct = default
    );
    Task<CardRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CardRelationship>> ListActiveByCardAsync(Guid cardId, CancellationToken ct = default);
    Task<CardRelationship?> FindActiveAsync(Guid sourceCardId, Guid targetCardId, RelationshipType type, CancellationToken ct = default);
    Task AddAsync(CardRelationship relationship, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task ArchiveRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
