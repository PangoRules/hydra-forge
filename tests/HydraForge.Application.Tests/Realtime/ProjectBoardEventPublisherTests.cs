namespace HydraForge.Application.Tests.Realtime;

using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Result = HydraForge.Domain.Common.Result;

public class ProjectBoardEventPublisherTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task PublishAsync_CalledOnSuccess()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        columnRepo.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateCardCommand(projectId, columnId, actorId, "New Card", "", CardType.Task, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Single(publisher.PublishedEvents);
        Assert.Equal(BoardAction.Created, publisher.PublishedEvents[0].Action);
        Assert.Equal(BoardEntityType.Card, publisher.PublishedEvents[0].EntityType);
        Assert.Equal(projectId, publisher.PublishedEvents[0].ProjectId);
    }

    [Fact]
    public async Task PublishAsync_NotCalledOnFailure()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        // No column added — create will fail validation
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateCardCommand(projectId, columnId, actorId, "New Card", "", CardType.Task, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Empty(publisher.PublishedEvents);
    }

    private static (
        InMemoryCardRepository cardRepo,
        InMemoryCardAssigneeRepository assigneeRepo,
        InMemoryCardWatcherRepository watcherRepo,
        InMemoryCardRelationshipRepository relationshipRepo,
        InMemoryColumnRepository columnRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemoryUserRepository userRepo,
        InMemoryAuditLogWriter auditWriter,
        NullSnapshotRefresher snapshotRefresher,
        FakePublisher publisher
    ) CreateMocks()
    {
        var publisher = new FakePublisher();
        return (
            new InMemoryCardRepository(),
            new InMemoryCardAssigneeRepository(),
            new InMemoryCardWatcherRepository(),
            new InMemoryCardRelationshipRepository(),
            new InMemoryColumnRepository(),
            new InMemoryProjectMemberRepository(),
            new InMemoryUserRepository(),
            new InMemoryAuditLogWriter(),
            new NullSnapshotRefresher(),
            publisher
        );
    }
}

internal class FakePublisher : IProjectBoardEventPublisher
{
    public List<ProjectBoardEventEnvelope> PublishedEvents { get; } = [];

    public Task PublishAsync(ProjectBoardEventEnvelope envelope, CancellationToken ct = default)
    {
        PublishedEvents.Add(envelope);
        return Task.CompletedTask;
    }
}

internal class InMemoryCardRepository : ICardRepository
{
    public List<Card> Cards { get; } = [];
    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult(Cards.FirstOrDefault(c => c.Id == cardId));
    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(Cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));
    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default) => Task.FromResult(Cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber && c.ArchivedAt == null));
    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Card>>(Cards.Where(c => c.ProjectId == projectId && (filter.ColumnId is null || c.ColumnId == filter.ColumnId.Value) && (filter.IncludeArchived || c.ArchivedAt == null) && (!filter.Type.HasValue || c.Type == filter.Type.Value)).OrderBy(c => c.Position).ToList());
    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult(Cards.Where(c => c.ProjectId == projectId && c.ArchivedAt == null).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());
    public Task AddAsync(Card card, CancellationToken ct = default) { Cards.Add(card); return Task.CompletedTask; }
    public void Add(Card card) => AddAsync(card).GetAwaiter().GetResult();
    public Task UpdateAsync(Card card, CancellationToken ct = default) { var idx = Cards.FindIndex(c => c.Id == card.Id); if (idx >= 0) Cards[idx] = card; return Task.CompletedTask; }
    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default) { foreach (var c in cards) { var idx = Cards.FindIndex(x => x.Id == c.Id); if (idx >= 0) Cards[idx] = c; } return Task.CompletedTask; }
    public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { Cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default) { foreach (var c in Cards.Where(c => c.ColumnId == columnId && c.Position > exceptPosition && c.ArchivedAt == null)) c.Position -= 1; return Task.CompletedTask; }
    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default) => Task.FromResult(Cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
}

internal class InMemoryCardAssigneeRepository : ICardAssigneeRepository
{
    public List<CardAssignee> Assignees { get; } = [];
    public Task<CardAssignee?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default) => Task.FromResult(Assignees.FirstOrDefault(a => a.CardId == cardId && a.UserId == userId));
    public Task<ILookup<Guid, CardAssignee>> ListByCardIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default) => Task.FromResult<ILookup<Guid, CardAssignee>>(Assignees.Where(a => cardIds.Contains(a.CardId)).ToLookup(a => a.CardId));
    public Task<IReadOnlyList<CardAssignee>> ListByCardAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardAssignee>>(Assignees.Where(a => a.CardId == cardId).ToList());
    public Task AddAsync(CardAssignee assignee, CancellationToken ct = default) { Assignees.Add(assignee); return Task.CompletedTask; }
    public Task AddRangeAsync(IReadOnlyList<CardAssignee> assignees, CancellationToken ct = default) { Assignees.AddRange(assignees); return Task.CompletedTask; }
    public void Add(CardAssignee assignee) => AddAsync(assignee).GetAwaiter().GetResult();
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { Assignees.RemoveAll(a => a.CardId == cardId && a.UserId == userId); return Task.CompletedTask; }
}

internal class InMemoryCardWatcherRepository : ICardWatcherRepository
{
    public List<CardWatcher> Watchers { get; } = [];
    public Task<CardWatcher?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default) => Task.FromResult(Watchers.FirstOrDefault(w => w.CardId == cardId && w.UserId == userId));
    public Task<ILookup<Guid, CardWatcher>> ListByCardIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default) => Task.FromResult<ILookup<Guid, CardWatcher>>(Watchers.Where(w => cardIds.Contains(w.CardId)).ToLookup(w => w.CardId));
    public Task<IReadOnlyList<CardWatcher>> ListByCardAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardWatcher>>(Watchers.Where(w => w.CardId == cardId).ToList());
    public Task AddAsync(CardWatcher watcher, CancellationToken ct = default) { Watchers.Add(watcher); return Task.CompletedTask; }
    public Task AddRangeAsync(IReadOnlyList<CardWatcher> watchers, CancellationToken ct = default) { Watchers.AddRange(watchers); return Task.CompletedTask; }
    public void Add(CardWatcher watcher) => AddAsync(watcher).GetAwaiter().GetResult();
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { Watchers.RemoveAll(w => w.CardId == cardId && w.UserId == userId); return Task.CompletedTask; }
}

internal class InMemoryCardRelationshipRepository : ICardRelationshipRepository
{
    public List<CardRelationship> Relationships { get; } = [];
    public Task<IReadOnlyList<CardRelationship>> ListByCardAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null).ToList());
    public Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => r.TargetCardId == cardId && r.Type == RelationshipType.BlockedBy && r.ArchivedAt == null).ToList());
    public Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => r.SourceCardId == cardId && r.Type == RelationshipType.Precedes && r.ArchivedAt == null).ToList());
    public Task<CardRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<CardRelationship?>(Relationships.FirstOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<CardRelationship>> ListActiveByCardAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null).ToList());
    public Task<CardRelationship?> FindActiveAsync(Guid sourceCardId, Guid targetCardId, RelationshipType type, CancellationToken ct = default) => Task.FromResult<CardRelationship?>(Relationships.FirstOrDefault(r => r.SourceCardId == sourceCardId && r.TargetCardId == targetCardId && r.Type == type && r.ArchivedAt == null));
    public Task AddAsync(CardRelationship relationship, CancellationToken ct = default) { Relationships.Add(relationship); return Task.CompletedTask; }
    public Task ArchiveAsync(Guid id, CancellationToken ct = default) { var rel = Relationships.FirstOrDefault(r => r.Id == id); if (rel != null) rel.ArchivedAt = DateTime.UtcNow; return Task.CompletedTask; }
    public Task ArchiveRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) { foreach (var rel in Relationships.Where(r => ids.Contains(r.Id))) rel.ArchivedAt = DateTime.UtcNow; return Task.CompletedTask; }
    public Task<IReadOnlyList<CardRelationship>> ListByProjectAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);
    public Task<IReadOnlyList<CardRelationship>> ListActiveByProjectAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);
    public void Add(CardRelationship relationship) => Relationships.Add(relationship);
}

internal class InMemoryColumnRepository : IColumnRepository
{
    public List<Column> Columns { get; } = [];
    public Task AddAsync(Column column, CancellationToken ct = default) { Columns.Add(column); return Task.CompletedTask; }
    public void Add(Column column) => AddAsync(column).GetAwaiter().GetResult();
    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Columns.FirstOrDefault(c => c.Id == id));
    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Column>>(Columns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToList());
    public Task UpdateAsync(Column column, CancellationToken ct = default) { var idx = Columns.FindIndex(c => c.Id == column.Id); if (idx >= 0) Columns[idx] = column; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { Columns.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default) { for (var i = 0; i < orderedColumnIds.Count; i++) { var col = Columns.FirstOrDefault(c => c.Id == orderedColumnIds[i]); if (col != null) col.Position = i; } return Task.CompletedTask; }
    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default) { Columns.AddRange(columns); return Task.CompletedTask; }
}

internal class InMemoryProjectMemberRepository : IProjectMemberRepository
{
    public List<ProjectMember> Members { get; } = [];
    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Members.FirstOrDefault(m => m.Id == id));
    public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default) => Task.FromResult(Members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));
    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ProjectMember>>(Members.Where(m => m.ProjectId == projectId).ToList());
    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default) { var idList = projectIds.ToList(); var counts = Members.Where(m => idList.Contains(m.ProjectId)).GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.Count()); return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts); }
    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) { Members.Add(member); return Task.CompletedTask; }
    public void Add(ProjectMember member) => AddMemberAsync(member).GetAwaiter().GetResult();
    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default) { var idx = Members.FindIndex(m => m.Id == member.Id); if (idx >= 0) Members[idx] = member; return Task.CompletedTask; }
    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { Members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
}

internal class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<Guid, User>>(Users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id));
    public Task<User?> FindByUsernameAsync(string username) => Task.FromResult(Users.FirstOrDefault(u => u.Username == username));
    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<string, User>>(Users.Where(u => usernames.Contains(u.Username, StringComparer.OrdinalIgnoreCase)).ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase));
    public Task<List<HydraForge.Domain.Entities.Auth.User>> SearchByUsernameAsync(string query, int maxResults = 10, CancellationToken ct = default)
        => Task.FromResult(new List<HydraForge.Domain.Entities.Auth.User>());
    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;
    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);
    public Task CreateAsync(User user) { Users.Add(user); return Task.CompletedTask; }
    public void Add(User user) => CreateAsync(user).GetAwaiter().GetResult();
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default) => Task.FromResult(Result.Success());
}
