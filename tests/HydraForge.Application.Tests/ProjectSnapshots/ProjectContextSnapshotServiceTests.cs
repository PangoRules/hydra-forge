using HydraForge.Application.Cards;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.ProjectSnapshots;

public class ProjectContextSnapshotServiceTests
{
    [Fact]
    public async Task RefreshAsync_CreatesSnapshotIfNoneExists()
    {
        var projectId = Guid.NewGuid();
        var (columnRepo, cardRepo, relationshipRepo, snapshotRepo) = CreateMocks();

        var columns = new List<Column>
        {
            new() { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Backlog", Position = 0 },
        };
        columnRepo.AddRange(columns);

        var service = new ProjectContextSnapshotService(columnRepo, cardRepo, relationshipRepo, snapshotRepo);

        await service.RefreshAsync(projectId);

        Assert.Single(snapshotRepo.AddedSnapshots);
        Assert.NotEmpty(snapshotRepo.AddedSnapshots[0].TemplateContent);
        Assert.Contains("Backlog", snapshotRepo.AddedSnapshots[0].TemplateContent);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesExistingSnapshot()
    {
        var projectId = Guid.NewGuid();
        var (columnRepo, cardRepo, relationshipRepo, snapshotRepo) = CreateMocks();

        var existingSnapshot = new ProjectContextSnapshot
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TemplateContent = "{}",
            TemplateGeneratedAt = DateTime.UtcNow.AddDays(-1),
        };
        snapshotRepo.AddSnapshot(existingSnapshot);

        var columns = new List<Column>
        {
            new() { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Done", Position = 0 },
        };
        columnRepo.AddRange(columns);

        var service = new ProjectContextSnapshotService(columnRepo, cardRepo, relationshipRepo, snapshotRepo);

        await service.RefreshAsync(projectId);

        Assert.Single(snapshotRepo.UpdatedSnapshots);
        Assert.Contains("Done", snapshotRepo.UpdatedSnapshots[0].TemplateContent);
        Assert.Equal(existingSnapshot.Id, snapshotRepo.UpdatedSnapshots[0].Id);
    }

    [Fact]
    public async Task RefreshAsync_RegeneratesTemplateContent()
    {
        var projectId = Guid.NewGuid();
        var (columnRepo, cardRepo, relationshipRepo, snapshotRepo) = CreateMocks();

        var colId = Guid.NewGuid();
        var columns = new List<Column>
        {
            new() { Id = colId, ProjectId = projectId, Name = "In Dev", Position = 0 },
        };
        columnRepo.AddRange(columns);

        var cardId = Guid.NewGuid();
        var cards = new List<Card>
        {
            new() { Id = cardId, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Test Card", Position = 0 },
        };
        cardRepo.AddRange(cards);

        var service = new ProjectContextSnapshotService(columnRepo, cardRepo, relationshipRepo, snapshotRepo);

        await service.RefreshAsync(projectId);

        Assert.Single(snapshotRepo.AddedSnapshots);
        var content = snapshotRepo.AddedSnapshots[0].TemplateContent;
        Assert.Contains("Test Card", content);
        Assert.Contains("In Dev", content);
    }

    private static (
        InMemoryColumnRepository columnRepo,
        InMemoryCardRepository cardRepo,
        InMemoryCardRelationshipRepository relationshipRepo,
        InMemorySnapshotRepository snapshotRepo
    ) CreateMocks()
    {
        return (
            new InMemoryColumnRepository(),
            new InMemoryCardRepository(),
            new InMemoryCardRelationshipRepository(),
            new InMemorySnapshotRepository()
        );
    }
}

internal class InMemoryColumnRepository : IColumnRepository
{
    private readonly List<Column> _columns = [];

    public void AddRange(List<Column> columns) => _columns.AddRange(columns);

    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Column>>(_columns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToList());

    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<Column?>(_columns.FirstOrDefault(c => c.Id == id));

    public Task AddAsync(Column column, CancellationToken ct = default)
    {
        _columns.Add(column);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        var idx = _columns.FindIndex(c => c.Id == column.Id);
        if (idx >= 0) _columns[idx] = column;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _columns.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default)
    {
        _columns.AddRange(columns);
        return Task.CompletedTask;
    }
}

internal class InMemoryCardRepository : ICardRepository
{
    private readonly List<Card> _cards = [];

    public void AddRange(List<Card> cards) => _cards.AddRange(cards);

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<Card?>(_cards.FirstOrDefault(c => c.Id == cardId));

    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(_cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));

    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult<Card?>(_cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber));

    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
    {
        var query = _cards.Where(c => c.ProjectId == projectId);
        if (!filter.IncludeArchived)
            query = query.Where(c => c.ArchivedAt == null);
        if (filter.ColumnId.HasValue)
            query = query.Where(c => c.ColumnId == filter.ColumnId.Value);
        return Task.FromResult<IReadOnlyList<Card>>(query.OrderBy(c => c.Position).ToList());
    }

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
    {
        var max = _cards.Where(c => c.ProjectId == projectId).Max(c => (int?)c.CardNumber);
        return Task.FromResult(max ?? 0);
    }

    public Task AddAsync(Card card, CancellationToken ct = default)
    {
        _cards.Add(card);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = _cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0) _cards[idx] = card;
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        foreach (var card in cards)
        {
            var idx = _cards.FindIndex(c => c.Id == card.Id);
            if (idx >= 0) _cards[idx] = card;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid cardId, CancellationToken ct = default)
    {
        _cards.RemoveAll(c => c.Id == cardId);
        return Task.CompletedTask;
    }

    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
        => Task.FromResult(_cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
}

internal class InMemoryCardRelationshipRepository : ICardRelationshipRepository
{
    private readonly List<CardRelationship> _relationships = [];

    public Task<IReadOnlyList<CardRelationship>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships);

    public Task<IReadOnlyList<CardRelationship>> ListActiveByProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => r.ArchivedAt == null).ToList());

    public Task<IReadOnlyList<CardRelationship>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => r.SourceCardId == cardId || r.TargetCardId == cardId).ToList());

    public Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => r.TargetCardId == cardId && r.Type == RelationshipType.BlockedBy && r.ArchivedAt == null).ToList());

    public Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => r.SourceCardId == cardId && r.Type == RelationshipType.Precedes && r.ArchivedAt == null).ToList());

    public Task<CardRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<CardRelationship?>(_relationships.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<CardRelationship>> ListActiveByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null).ToList());

    public Task<CardRelationship?> FindActiveAsync(Guid sourceCardId, Guid targetCardId, RelationshipType type, CancellationToken ct = default)
        => Task.FromResult<CardRelationship?>(_relationships.FirstOrDefault(r => r.SourceCardId == sourceCardId && r.TargetCardId == targetCardId && r.Type == type && r.ArchivedAt == null));

    public Task AddAsync(CardRelationship relationship, CancellationToken ct = default)
    {
        _relationships.Add(relationship);
        return Task.CompletedTask;
    }

    public Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var rel = _relationships.FirstOrDefault(r => r.Id == id);
        if (rel != null) rel.ArchivedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task ArchiveRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        foreach (var id in ids)
        {
            var rel = _relationships.FirstOrDefault(r => r.Id == id);
            if (rel != null) rel.ArchivedAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }
}

internal class InMemorySnapshotRepository : IProjectContextSnapshotRepository
{
    private readonly List<ProjectContextSnapshot> _snapshots = [];

    public List<ProjectContextSnapshot> AddedSnapshots { get; } = [];
    public List<ProjectContextSnapshot> UpdatedSnapshots { get; } = [];

    public void AddSnapshot(ProjectContextSnapshot snapshot) => _snapshots.Add(snapshot);

    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<ProjectContextSnapshot?>(_snapshots.FirstOrDefault(s => s.ProjectId == projectId));

    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        AddedSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        UpdatedSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }
}