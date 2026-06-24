namespace HydraForge.Application.Tests.Cards;

using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Result = HydraForge.Domain.Common.Result;

public class CardRelationshipServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    private static (InMemoryCardRelationshipRepository2 relationshipRepo, InMemoryCardRepository2 cardRepo, InMemoryProjectMemberRepository memberRepo, InMemoryAuditLogWriter auditWriter, NullSnapshotRefresher snapshotRefresher, FakeProjectBoardEventPublisher publisher, CardRelationshipService service) CreateService()
    {
        var relationshipRepo = new InMemoryCardRelationshipRepository2();
        var cardRepo = new InMemoryCardRepository2();
        var memberRepo = new InMemoryProjectMemberRepository();
        var auditWriter = new InMemoryAuditLogWriter();
        var snapshotRefresher = new NullSnapshotRefresher();
        var publisher = new FakeProjectBoardEventPublisher();
        var service = new CardRelationshipService(relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher);
        return (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service);
    }

    [Fact]
    public async Task CreateAsync_self_link_rejected()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, Title = "Card", CardNumber = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardId, cardId, RelationshipType.BlockedBy, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Relationships.SelfDenied, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_cross_project_denied()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var otherProjectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = otherProjectId, Title = "Card B", CardNumber = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Relationships.CrossProjectDenied, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_duplicate_active_rejected()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result1 = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));
        Assert.True(result1.IsSuccess);

        var result2 = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));
        Assert.True(result2.IsFailure);
        Assert.Equal(DomainErrorCodes.Relationships.Duplicate, result2.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_blockedby_cycle_rejected()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        // A blocked by B
        var r1 = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardB, cardA, RelationshipType.BlockedBy, actorId));
        Assert.True(r1.IsSuccess);

        // Now A blocked by B already exists; trying B blocked by A creates cycle
        var r2 = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));
        Assert.True(r2.IsFailure);
        Assert.Equal(DomainErrorCodes.Relationships.Cycle, r2.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_precedes_cycle_rejected()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        // A precedes B
        var r1 = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.Precedes, actorId));
        Assert.True(r1.IsSuccess);

        // Now B precedes A creates cycle
        var r2 = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardB, cardA, RelationshipType.Precedes, actorId));
        Assert.True(r2.IsFailure);
        Assert.Equal(DomainErrorCodes.Relationships.Cycle, r2.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_relates_does_not_participate_in_cycle()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        // A blocked by B
        var r1 = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardB, cardA, RelationshipType.BlockedBy, actorId));
        Assert.True(r1.IsSuccess);

        // A relates B — no cycle (Relates does not participate)
        var r2 = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.Relates, actorId));
        Assert.True(r2.IsSuccess);
    }

    [Fact]
    public async Task ListAsync_returns_active_only()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));
        var result = await service.ListAsync(projectId, cardA, actorId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Relationships);
        Assert.Equal(RelationshipType.BlockedBy, result.Value.Relationships[0].Type);
    }

    [Fact]
    public async Task DeleteAsync_archives_relationship()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var createResult = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));
        Assert.True(createResult.IsSuccess);
        var relId = createResult.Value.Id;

        var deleteResult = await service.DeleteAsync(new DeleteRelationshipCommand(projectId, cardA, relId, actorId));
        Assert.True(deleteResult.IsSuccess);

        // List should now return empty
        var listResult = await service.ListAsync(projectId, cardA, actorId);
        Assert.Empty(listResult.Value.Relationships);
    }

    [Fact]
    public async Task GetArchiveImpactAsync_returns_dependent_cards()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        // Card A blocks Card B — preflight always returns dependent list, regardless of confirm
        await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));

        var result = await service.GetArchiveImpactAsync(new ArchiveImpactCommand(projectId, cardA, false, actorId));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.DependentCards);
        Assert.Equal(cardB, result.Value.DependentCards[0].Id);
    }

    [Fact]
    public async Task ArchiveCardWithRelationshipsAsync_archives_card_and_relationships()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();
        var colId = NewId();

        var cardARecord = new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, Title = "Card A", CardNumber = 1, Position = 0 };
        var cardBRecord = new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, Title = "Card B", CardNumber = 2, Position = 1 };
        cardRepo.Add(cardARecord);
        cardRepo.Add(cardBRecord);
        cardRepo.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));

        var result = await service.ArchiveCardWithRelationshipsAsync(new ArchiveImpactCommand(projectId, cardA, true, actorId));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.DependentCards);

        // Card A should be archived
        var archivedCard = cardRepo.GetById(cardA);
        Assert.NotNull(archivedCard!.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveCardWithRelationshipsAsync_confirm_required_when_dependents_exist()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();
        var colId = NewId();

        var cardARecord = new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, Title = "Card A", CardNumber = 1, Position = 0 };
        var cardBRecord = new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, Title = "Card B", CardNumber = 2, Position = 1 };
        cardRepo.Add(cardARecord);
        cardRepo.Add(cardBRecord);
        cardRepo.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));

        var result = await service.ArchiveCardWithRelationshipsAsync(new ArchiveImpactCommand(projectId, cardA, false, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Relationships.ArchiveImpactConfirmRequired, result.Error.Code);

        // Card A should NOT be archived
        var notArchived = cardRepo.GetById(cardA);
        Assert.Null(notArchived!.ArchivedAt);
    }

    [Fact]
    public async Task CreateAsync_success()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.Precedes, actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(cardA, result.Value.SourceCardId);
        Assert.Equal(cardB, result.Value.TargetCardId);
        Assert.Equal(RelationshipType.Precedes, result.Value.Type);
    }

    // ─── Audit tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WritesAuditLog()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("CardRelationship", req.EntityType);
        Assert.Equal(result.Value.Id, req.EntityId);
        Assert.Equal("Created", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task DeleteAsync_WritesAuditLog()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, Title = "Card A", CardNumber = 1 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, Title = "Card B", CardNumber = 2 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var createResult = await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));
        Assert.True(createResult.IsSuccess);
        var relId = createResult.Value.Id;
        auditWriter.Clear();

        var deleteResult = await service.DeleteAsync(new DeleteRelationshipCommand(projectId, cardA, relId, actorId));
        Assert.True(deleteResult.IsSuccess);

        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("CardRelationship", req.EntityType);
        Assert.Equal(relId, req.EntityId);
        Assert.Equal("Deleted", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task ArchiveCardWithRelationshipsAsync_WritesAuditLog()
    {
        var (relationshipRepo, cardRepo, memberRepo, auditWriter, snapshotRefresher, publisher, service) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        var cardA = NewId();
        var cardB = NewId();
        var colId = NewId();

        cardRepo.Add(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, Title = "Card A", CardNumber = 1, Position = 0 });
        cardRepo.Add(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, Title = "Card B", CardNumber = 2, Position = 1 });
        cardRepo.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        await service.CreateAsync(new CreateRelationshipCommand(projectId, cardA, cardB, RelationshipType.BlockedBy, actorId));

        var result = await service.ArchiveCardWithRelationshipsAsync(new ArchiveImpactCommand(projectId, cardA, true, actorId));

        Assert.True(result.IsSuccess);
        var req = auditWriter.Writes.Single(r => r.Action == "ArchivedWithRelationships");
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Card", req.EntityType);
        Assert.Equal(cardA, req.EntityId);
        Assert.Equal("ArchivedWithRelationships", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    // ─── In-memory repositories ─────────────────────────────────────────────

    private class InMemoryCardRelationshipRepository2 : ICardRelationshipRepository
    {
        public List<CardRelationship> Relationships { get; } = [];

        public Task<IReadOnlyList<CardRelationship>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null).ToList());

        public Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(Guid cardId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => r.TargetCardId == cardId && r.Type == RelationshipType.BlockedBy && r.ArchivedAt == null).ToList());

        public Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(Guid cardId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => r.SourceCardId == cardId && r.Type == RelationshipType.Precedes && r.ArchivedAt == null).ToList());

        public Task<CardRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<CardRelationship?>(Relationships.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<CardRelationship>> ListActiveByCardAsync(Guid cardId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null).ToList());

        public Task<CardRelationship?> FindActiveAsync(Guid sourceCardId, Guid targetCardId, RelationshipType type, CancellationToken ct = default)
            => Task.FromResult<CardRelationship?>(Relationships.FirstOrDefault(r => r.SourceCardId == sourceCardId && r.TargetCardId == targetCardId && r.Type == type && r.ArchivedAt == null));

        public Task AddAsync(CardRelationship relationship, CancellationToken ct = default) { Relationships.Add(relationship); return Task.CompletedTask; }
        public void Add(CardRelationship relationship) => Relationships.Add(relationship);

        public Task ArchiveAsync(Guid id, CancellationToken ct = default)
        {
            var rel = Relationships.FirstOrDefault(r => r.Id == id);
            if (rel != null) rel.ArchivedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task ArchiveRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        {
            foreach (var rel in Relationships.Where(r => ids.Contains(r.Id)))
                rel.ArchivedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CardRelationship>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CardRelationship>>([]);

        public Task<IReadOnlyList<CardRelationship>> ListActiveByProjectAsync(Guid projectId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CardRelationship>>([]);
    }

    private class InMemoryCardRepository2 : ICardRepository
    {
        public List<Card> Cards { get; } = [];
        public List<Column> Columns { get; } = [];

        public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
            => Task.FromResult<Card?>(Cards.FirstOrDefault(c => c.Id == cardId));
        public Card? GetById(Guid cardId) => Cards.FirstOrDefault(c => c.Id == cardId);

        public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(Cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));

        public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
            => Task.FromResult<Card?>(Cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber));

        public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>(Cards.Where(c => c.ProjectId == projectId && (filter.IncludeArchived || c.ArchivedAt == null) && (filter.ColumnId == null || c.ColumnId == filter.ColumnId.Value)).ToList());

        public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
            => Task.FromResult(Cards.Where(c => c.ProjectId == projectId).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());

        public Task AddAsync(Card card, CancellationToken ct = default) { Cards.Add(card); return Task.CompletedTask; }
        public void Add(Card card) => Cards.Add(card);
        public Task UpdateAsync(Card card, CancellationToken ct = default)
        {
            var idx = Cards.FindIndex(c => c.Id == card.Id);
            if (idx >= 0) Cards[idx] = card;
            return Task.CompletedTask;
        }
        public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
        {
            foreach (var card in cards)
            {
                var idx = Cards.FindIndex(c => c.Id == card.Id);
                if (idx >= 0) Cards[idx] = card;
            }
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { Cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
        public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
            => Task.FromResult(Cards.Count(c => c.ColumnId == columnId));
        public void AddColumn(Column column) => Columns.Add(column);
    }

    private class InMemoryProjectMemberRepository : IProjectMemberRepository
    {
        public List<ProjectMember> Members { get; } = [];

        public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Members.FirstOrDefault(m => m.Id == id));
        public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(Members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));
        public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProjectMember>>(Members.Where(m => m.ProjectId == projectId).ToList());
        public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
        {
            var idList = projectIds.ToList();
            var counts = Members.Where(m => idList.Contains(m.ProjectId)).GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.Count());
            return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
        }
        public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) { Members.Add(member); return Task.CompletedTask; }
        public void Add(ProjectMember member) => Members.Add(member);
        public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
        {
            var idx = Members.FindIndex(m => m.Id == member.Id);
            if (idx >= 0) Members[idx] = member;
            return Task.CompletedTask;
        }
        public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { Members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
    }

    private class InMemoryAuditLogWriter : IAuditLogWriter
    {
        public List<AuditLogRequest> Writes { get; } = [];

        public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
        {
            Writes.Add(request);
            return Task.FromResult(Result.Success());
        }

        public void Clear() => Writes.Clear();
    }
}
