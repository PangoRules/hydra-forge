namespace HydraForge.Application.Tests.Cards;

using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Result = HydraForge.Domain.Common.Result;

public class CardServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_AssignsMaxCardNumberPlus1()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 3, Title = "Existing" });
        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 7, Title = "Archived", ArchivedAt = DateTime.UtcNow });
        columnRepo.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateCardCommand(projectId, columnId, actorId, "New Card", "", CardType.Task, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.CardNumber);
    }

    [Fact]
    public async Task CreateAsync_AppendsToTargetColumn()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        columnRepo.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Position = 0, Title = "First" });
        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Position = 1, Title = "Second" });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateCardCommand(projectId, columnId, actorId, "Third Card", "", CardType.Task, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);
    }

    [Fact]
    public async Task CreateAsync_NeverReusesArchivedCardNumbers()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 5, ArchivedAt = DateTime.UtcNow });
        columnRepo.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);

        var result = await service.CreateAsync(new CreateCardCommand(projectId, columnId, actorId, "New Card", "", CardType.Task, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CardNumber);
    }

    [Fact]
    public async Task ListAsync_FiltersByColumn()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var col1 = NewId();
        var col2 = NewId();

        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = col1, CardNumber = 1, Title = "Card1" });
        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = col2, CardNumber = 2, Title = "Card2" });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.ListAsync(projectId, new CardListFilter(col1), actorId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Card1", result.Value[0].Title);
    }

    [Fact]
    public async Task ListAsync_FiltersArchived()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Active" });
        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Title = "Archived", ArchivedAt = DateTime.UtcNow });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var resultWithoutArchived = await service.ListAsync(projectId, new CardListFilter(IncludeArchived: false), actorId);
        Assert.Single(resultWithoutArchived.Value);

        var resultWithArchived = await service.ListAsync(projectId, new CardListFilter(IncludeArchived: true), actorId);
        Assert.Equal(2, resultWithArchived.Value.Count);
    }

    [Fact]
    public async Task ListAsync_FiltersByType()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Type = CardType.Task, Title = "Task" });
        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Type = CardType.Bug, Title = "Bug" });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.ListAsync(projectId, new CardListFilter(Type: CardType.Task), actorId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Task", result.Value[0].Title);
    }

    [Fact]
    public async Task ListAsync_FiltersByAssignee()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var assigneeId = NewId();
        var columnId = NewId();
        var assignedCardId = NewId();
        var unassignedCardId = NewId();

        cardRepo.Add(new Card { Id = assignedCardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Assigned" });
        cardRepo.Add(new Card { Id = unassignedCardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Title = "Unassigned" });
        assigneeRepo.Add(new CardAssignee { CardId = assignedCardId, UserId = assigneeId, AssignedByUserId = actorId });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.ListAsync(projectId, new CardListFilter(AssigneeUserId: assigneeId), actorId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Assigned", result.Value[0].Title);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingCard_ReturnsCard()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Test Card" });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.GetByIdAsync(projectId, cardId, actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Test Card", result.Value.Title);
    }

    [Fact]
    public async Task GetByNumberAsync_ExistingNumber_ReturnsCard()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();

        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = NewId(), CardNumber = 42, Title = "Card 42" });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.GetByNumberAsync(projectId, 42, actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Card 42", result.Value.Title);
    }

    [Fact]
    public async Task UpdateAsync_IncrementsVersion()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var columnId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Original", Version = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UpdateAsync(new UpdateCardCommand(projectId, cardId, actorId, "Updated", "", CardType.Task, null, null, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Version);
    }

    [Fact]
    public async Task MoveAsync_CompactsOldColumnAndSetsMovedAt()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var oldColumnId = NewId();
        var newColumnId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = oldColumnId, CardNumber = 1, Position = 1, Version = 1, MovedAt = DateTime.UtcNow.AddDays(-1) });
        columnRepo.Add(new Column { Id = newColumnId, ProjectId = projectId, Name = "Done", Position = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.MoveAsync(new MoveCardCommand(projectId, cardId, newColumnId, 0, actorId, false, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(newColumnId, result.Value.ColumnId);
        Assert.Equal(0, result.Value.Position);
        Assert.True(result.Value.MovedAt > DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task MoveAsync_BlockedCardWithoutConfirm_ReturnsWarningResult()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var blockerCardId = NewId();
        var columnId = NewId();

        var card = new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Position = 1, Version = 1 };
        cardRepo.Add(card);
        cardRepo.Add(new Card { Id = blockerCardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Title = "Blocker" });
        relationshipRepo.Add(new CardRelationship { SourceCardId = blockerCardId, TargetCardId = cardId, Type = RelationshipType.BlockedBy });
        columnRepo.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.MoveAsync(new MoveCardCommand(projectId, cardId, columnId, 0, actorId, false, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.BlockedMoveWarning, result.Error.Code);
    }

    [Fact]
    public async Task MoveAsync_PredecessorWithoutConfirm_ReturnsWarningResult()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var predecessorCardId = NewId();
        var columnId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Position = 1, Version = 1 });
        cardRepo.Add(new Card { Id = predecessorCardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Title = "Predecessor" });
        relationshipRepo.Add(new CardRelationship { SourceCardId = cardId, TargetCardId = predecessorCardId, Type = RelationshipType.Precedes });
        columnRepo.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.MoveAsync(new MoveCardCommand(projectId, cardId, columnId, 0, actorId, false, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.BlockedMoveWarning, result.Error.Code);
    }

    [Fact]
    public async Task MoveAsync_ConfirmBlockedMove_Succeeds()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var blockerCardId = NewId();
        var columnId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Position = 1, Version = 1 });
        cardRepo.Add(new Card { Id = blockerCardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Title = "Blocker" });
        relationshipRepo.Add(new CardRelationship { SourceCardId = blockerCardId, TargetCardId = cardId, Type = RelationshipType.BlockedBy });
        columnRepo.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.MoveAsync(new MoveCardCommand(projectId, cardId, columnId, 0, actorId, ConfirmBlockedMove: true, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Position);
    }

    [Fact]
    public async Task AssignAsync_CreatesCardAssigneeAndCardWatcher()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var assigneeUserId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Test" });
        userRepo.Add(new User { Id = assigneeUserId, Username = "assignee" });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.AssignAsync(new AssignCardCommand(projectId, cardId, assigneeUserId, actorId));

        Assert.True(result.IsSuccess);
        Assert.Single(assigneeRepo.Assignees);
        Assert.Single(watcherRepo.Watchers);
    }

    [Fact]
    public async Task AssignAsync_DuplicateAssign_ReturnsError()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var assigneeUserId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Test" });
        userRepo.Add(new User { Id = assigneeUserId, Username = "assignee" });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        assigneeRepo.Add(new CardAssignee { CardId = cardId, UserId = assigneeUserId, AssignedByUserId = actorId });

        var result = await service.AssignAsync(new AssignCardCommand(projectId, cardId, assigneeUserId, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.DuplicateAssignee, result.Error.Code);
    }

    [Fact]
    public async Task UnassignAsync_RemovesAssigneeOnly()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var assigneeUserId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Test" });
        assigneeRepo.Add(new CardAssignee { CardId = cardId, UserId = assigneeUserId, AssignedByUserId = actorId });
        watcherRepo.Add(new CardWatcher { CardId = cardId, UserId = assigneeUserId });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UnassignAsync(new UnassignCardCommand(projectId, cardId, assigneeUserId, actorId));

        Assert.True(result.IsSuccess);
        Assert.Empty(assigneeRepo.Assignees);
        Assert.Single(watcherRepo.Watchers);
    }

    [Fact]
    public async Task ArchiveAsync_SetsArchivedAtAndCompactsPositions()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var columnId = NewId();

        var card = new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Position = 1, Version = 1 };
        cardRepo.Add(card);
        cardRepo.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Position = 2, Title = "Below" });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.ArchiveAsync(new ArchiveCardCommand(projectId, cardId, actorId, 1));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ArchivedAt);
        var belowCard = cardRepo.Cards.First(c => c.CardNumber == 2);
        Assert.Equal(1, belowCard.Position);
    }

    [Fact]
    public async Task DeleteAsync_HardDeletesOnlyIfAllowed()
    {
        var (cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new CardService(cardRepo, assigneeRepo, watcherRepo, relationshipRepo, columnRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var cardId = NewId();
        var columnId = NewId();

        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Position = 0, Version = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.DeleteAsync(new DeleteCardCommand(projectId, cardId, actorId));

        Assert.True(result.IsSuccess);
        Assert.Empty(cardRepo.Cards);
    }

    private static (
        InMemoryCardRepository cardRepo,
        InMemoryCardAssigneeRepository assigneeRepo,
        InMemoryCardWatcherRepository watcherRepo,
        InMemoryCardRelationshipRepository relationshipRepo,
        InMemoryColumnRepository columnRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemoryUserRepository userRepo,
        InMemoryAuditLogWriter auditWriter
    ) CreateMocks()
    {
        return (
            new InMemoryCardRepository(),
            new InMemoryCardAssigneeRepository(),
            new InMemoryCardWatcherRepository(),
            new InMemoryCardRelationshipRepository(),
            new InMemoryColumnRepository(),
            new InMemoryProjectMemberRepository(),
            new InMemoryUserRepository(),
            new InMemoryAuditLogWriter()
        );
    }
}

internal class InMemoryCardRepository : ICardRepository
{
    public List<Card> Cards { get; } = [];

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(Cards.FirstOrDefault(c => c.Id == cardId));

    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult(Cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber && c.ArchivedAt == null));

    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
    {
        var query = Cards.Where(c => c.ProjectId == projectId);
        if (filter.ColumnId.HasValue)
            query = query.Where(c => c.ColumnId == filter.ColumnId.Value);
        if (!filter.IncludeArchived)
            query = query.Where(c => c.ArchivedAt == null);
        if (filter.Type.HasValue)
            query = query.Where(c => c.Type == filter.Type.Value);
        return Task.FromResult<IReadOnlyList<Card>>(query.OrderBy(c => c.Position).ToList());
    }

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult(Cards.Where(c => c.ProjectId == projectId && c.ArchivedAt == null).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());

    public Task AddAsync(Card card, CancellationToken ct = default) { Cards.Add(card); return Task.CompletedTask; }
    public void Add(Card card) => AddAsync(card).GetAwaiter().GetResult();
    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = Cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0) Cards[idx] = card;
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { Cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default)
    {
        var toCompact = Cards.Where(c => c.ColumnId == columnId && c.Position > exceptPosition && c.ArchivedAt == null).ToList();
        foreach (var c in toCompact) c.Position -= 1;
        return Task.CompletedTask;
    }
    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
        => Task.FromResult(Cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
}

internal class InMemoryCardAssigneeRepository : ICardAssigneeRepository
{
    public List<CardAssignee> Assignees { get; } = [];

    public Task<CardAssignee?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Assignees.FirstOrDefault(a => a.CardId == cardId && a.UserId == userId));

    public Task<IReadOnlyList<CardAssignee>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardAssignee>>(Assignees.Where(a => a.CardId == cardId).ToList());

    public Task AddAsync(CardAssignee assignee, CancellationToken ct = default) { Assignees.Add(assignee); return Task.CompletedTask; }
    public void Add(CardAssignee assignee) => AddAsync(assignee).GetAwaiter().GetResult();
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { Assignees.RemoveAll(a => a.CardId == cardId && a.UserId == userId); return Task.CompletedTask; }
}

internal class InMemoryCardWatcherRepository : ICardWatcherRepository
{
    public List<CardWatcher> Watchers { get; } = [];

    public Task<CardWatcher?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Watchers.FirstOrDefault(w => w.CardId == cardId && w.UserId == userId));

    public Task<IReadOnlyList<CardWatcher>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardWatcher>>(Watchers.Where(w => w.CardId == cardId).ToList());

    public Task AddAsync(CardWatcher watcher, CancellationToken ct = default) { Watchers.Add(watcher); return Task.CompletedTask; }
    public void Add(CardWatcher watcher) => AddAsync(watcher).GetAwaiter().GetResult();
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { Watchers.RemoveAll(w => w.CardId == cardId && w.UserId == userId); return Task.CompletedTask; }
}

internal class InMemoryCardRelationshipRepository : ICardRelationshipRepository
{
    public List<CardRelationship> Relationships { get; } = [];

    public Task<IReadOnlyList<CardRelationship>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => r.SourceCardId == cardId || r.TargetCardId == cardId).ToList());

    public Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => r.TargetCardId == cardId && r.Type == RelationshipType.BlockedBy).ToList());

    public Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(Relationships.Where(r => r.SourceCardId == cardId && r.Type == RelationshipType.Precedes).ToList());

    public void Add(CardRelationship relationship) => Relationships.Add(relationship);
}

internal class InMemoryColumnRepository : IColumnRepository
{
    public List<Column> Columns { get; } = [];

    public Task AddAsync(Column column, CancellationToken ct = default) { Columns.Add(column); return Task.CompletedTask; }
    public void Add(Column column) => AddAsync(column).GetAwaiter().GetResult();
    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Columns.FirstOrDefault(c => c.Id == id));
    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Column>>(Columns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToList());
    public Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        var idx = Columns.FindIndex(c => c.Id == column.Id);
        if (idx >= 0) Columns[idx] = column;
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { Columns.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default)
    {
        for (var i = 0; i < orderedColumnIds.Count; i++)
        {
            var col = Columns.FirstOrDefault(c => c.Id == orderedColumnIds[i]);
            if (col != null) col.Position = i;
        }
        return Task.CompletedTask;
    }
    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default) { Columns.AddRange(columns); return Task.CompletedTask; }
}

internal class InMemoryProjectMemberRepository : IProjectMemberRepository
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
    public void Add(ProjectMember member) => AddMemberAsync(member).GetAwaiter().GetResult();
    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = Members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0) Members[idx] = member;
        return Task.CompletedTask;
    }
    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { Members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
}

internal class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> FindByIdAsync(Guid id) => GetByIdAsync(id);
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
    public Task<User?> FindByUsernameAsync(string username)
        => Task.FromResult(Users.FirstOrDefault(u => u.Username == username));
    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;
    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);
    public Task CreateAsync(User user) { Users.Add(user); return Task.CompletedTask; }
    public void Add(User user) => CreateAsync(user).GetAwaiter().GetResult();
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default) => Task.FromResult(Result.Success());
}
