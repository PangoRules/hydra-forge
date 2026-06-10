using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Checklist;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.Checklist;

public class ChecklistServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_AppendsAtMaxPosition_WhenNoPositionProvided()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        repo.Items.Add(new ChecklistItem { Id = NewId(), CardId = cardId, Text = "First", Position = 0 });
        repo.Items.Add(new ChecklistItem { Id = NewId(), CardId = cardId, Text = "Second", Position = 1 });

        var result = await service.CreateAsync(new CreateChecklistItemCommand(projectId, cardId, actorId, "Third", null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);
        Assert.Equal("Third", result.Value.Text);
        Assert.False(result.Value.IsCompleted);
    }

    [Fact]
    public async Task CreateAsync_InsertsAtSpecifiedPosition_ShiftsSubsequent()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var item0 = new ChecklistItem { Id = NewId(), CardId = cardId, Text = "Zero", Position = 0 };
        var item1 = new ChecklistItem { Id = NewId(), CardId = cardId, Text = "One", Position = 1 };
        var item2 = new ChecklistItem { Id = NewId(), CardId = cardId, Text = "Two", Position = 2 };

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        repo.Items.Add(item0);
        repo.Items.Add(item1);
        repo.Items.Add(item2);

        var result = await service.CreateAsync(new CreateChecklistItemCommand(projectId, cardId, actorId, "Inserted", null, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Position);
        var items = (await repo.ListByCardAsync(cardId)).OrderBy(i => i.Position).ToList();
        Assert.Equal("Zero", items[0].Text);
        Assert.Equal("Inserted", items[1].Text);
        Assert.Equal("One", items[2].Text);
        Assert.Equal("Two", items[3].Text);
    }

    [Fact]
    public async Task CreateAsync_InvalidPosition_ReturnsFailure()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        repo.Items.Add(new ChecklistItem { Id = NewId(), CardId = cardId, Text = "Only", Position = 0 });

        var result = await service.CreateAsync(new CreateChecklistItemCommand(projectId, cardId, actorId, "Bad", null, 5));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Checklist.InvalidPosition, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_AssigneeNotMember_ReturnsInvalidAssignee()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var nonMemberId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateChecklistItemCommand(projectId, cardId, actorId, "WithAssignee", nonMemberId, null));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Checklist.InvalidAssignee, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_AssigneeDisabled_ReturnsInvalidAssignee()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var assigneeId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = assigneeId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = assigneeId, Username = "disableduser", Email = "a@a.com", PasswordHash = "x", IsDisabled = true });

        var result = await service.CreateAsync(new CreateChecklistItemCommand(projectId, cardId, actorId, "WithAssignee", assigneeId, null));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Checklist.InvalidAssignee, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ValidAssignee_ReturnsDtoWithUsername()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var assigneeId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = assigneeId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = assigneeId, Username = "alice", Email = "a@a.com", PasswordHash = "x", IsDisabled = false });

        var result = await service.CreateAsync(new CreateChecklistItemCommand(projectId, cardId, actorId, "Task", assigneeId, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(assigneeId, result.Value.AssignedTo);
        Assert.Equal("alice", result.Value.AssignedToUsername);
    }

    [Fact]
    public async Task CreateAsync_NonMember_ReturnsMembershipDenied()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var nonMemberId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });

        var result = await service.CreateAsync(new CreateChecklistItemCommand(projectId, cardId, nonMemberId, "Text", null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_CardNotInProject_ReturnsNotFound()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var otherProjectId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = otherProjectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateChecklistItemCommand(projectId, cardId, actorId, "Text", null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTextAndAssignee()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var itemId = NewId();
        var assigneeId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = assigneeId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = assigneeId, Username = "bob", Email = "b@b.com", PasswordHash = "x", IsDisabled = false });
        repo.Items.Add(new ChecklistItem { Id = itemId, CardId = cardId, Text = "Old", Position = 0 });

        var result = await service.UpdateAsync(new UpdateChecklistItemCommand(projectId, cardId, itemId, actorId, "NewText", assigneeId));

        Assert.True(result.IsSuccess);
        Assert.Equal("NewText", result.Value.Text);
        Assert.Equal(assigneeId, result.Value.AssignedTo);
        Assert.Equal("bob", result.Value.AssignedToUsername);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsItemNotFound()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UpdateAsync(new UpdateChecklistItemCommand(projectId, cardId, NewId(), actorId, "Text", null));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Checklist.ItemNotFound, result.Error.Code);
    }

    [Fact]
    public async Task ToggleAsync_TogglesCompletion()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var itemId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        repo.Items.Add(new ChecklistItem { Id = itemId, CardId = cardId, Text = "Task", Position = 0, IsCompleted = false });

        var result = await service.ToggleAsync(new ToggleChecklistItemCommand(projectId, cardId, itemId, actorId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsCompleted);
    }

    [Fact]
    public async Task ReorderAsync_MovesItem_ShiftsOthers()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var item0 = new ChecklistItem { Id = NewId(), CardId = cardId, Text = "A", Position = 0 };
        var item1 = new ChecklistItem { Id = NewId(), CardId = cardId, Text = "B", Position = 1 };
        var item2 = new ChecklistItem { Id = NewId(), CardId = cardId, Text = "C", Position = 2 };

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        repo.Items.Add(item0);
        repo.Items.Add(item1);
        repo.Items.Add(item2);

        var result = await service.ReorderAsync(new ReorderChecklistItemCommand(projectId, cardId, item0.Id, actorId, 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);
        var items = (await repo.ListByCardAsync(cardId)).OrderBy(i => i.Position).ToList();
        Assert.Equal("B", items[0].Text);
        Assert.Equal("C", items[1].Text);
        Assert.Equal("A", items[2].Text);
    }

    [Fact]
    public async Task ReorderAsync_InvalidPosition_ReturnsFailure()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var itemId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        repo.Items.Add(new ChecklistItem { Id = itemId, CardId = cardId, Text = "A", Position = 0 });
        repo.Items.Add(new ChecklistItem { Id = NewId(), CardId = cardId, Text = "B", Position = 1 });

        var result = await service.ReorderAsync(new ReorderChecklistItemCommand(projectId, cardId, itemId, actorId, 5));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Checklist.InvalidPosition, result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_RemovesItem_CompactsPositions()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var itemId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        repo.Items.Add(new ChecklistItem { Id = itemId, CardId = cardId, Text = "DeleteMe", Position = 1 });
        repo.Items.Add(new ChecklistItem { Id = NewId(), CardId = cardId, Text = "After", Position = 2 });

        var result = await service.DeleteAsync(new DeleteChecklistItemCommand(projectId, cardId, itemId, actorId));

        Assert.True(result.IsSuccess);
        var remaining = (await repo.ListByCardAsync(cardId)).OrderBy(i => i.Position).ToList();
        Assert.Single(remaining);
        Assert.Equal(0, remaining[0].Position);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllItemsOrderedByPosition()
    {
        var (repo, cardRepo, memberRepo, userRepo, auditWriter) = CreateMocks();
        var service = new ChecklistService(repo, cardRepo, memberRepo, userRepo, auditWriter);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        repo.Items.Add(new ChecklistItem { Id = NewId(), CardId = cardId, Text = "B", Position = 1 });
        repo.Items.Add(new ChecklistItem { Id = NewId(), CardId = cardId, Text = "A", Position = 0 });

        var result = await service.ListAsync(projectId, cardId, actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("A", result.Value[0].Text);
        Assert.Equal("B", result.Value[1].Text);
    }

    private static (InMemoryChecklistItemRepository, InMemoryCardRepository, InMemoryProjectMemberRepository, InMemoryUserRepository, InMemoryAuditLogWriter) CreateMocks()
    {
        var repo = new InMemoryChecklistItemRepository();
        var cardRepo = new InMemoryCardRepository();
        var memberRepo = new InMemoryProjectMemberRepository();
        var userRepo = new InMemoryUserRepository();
        var auditWriter = new InMemoryAuditLogWriter();
        return (repo, cardRepo, memberRepo, userRepo, auditWriter);
    }
}

internal class InMemoryChecklistItemRepository : IChecklistItemRepository
{
    public List<ChecklistItem> Items { get; } = [];

    public Task<ChecklistItem?> GetByIdAsync(Guid itemId, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(i => i.Id == itemId));

    public Task<IReadOnlyList<ChecklistItem>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ChecklistItem>>(Items.Where(i => i.CardId == cardId).OrderBy(i => i.Position).ToList());

    public Task<int> GetMaxPositionAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(Items.Where(i => i.CardId == cardId).Select(i => i.Position).DefaultIfEmpty(-1).Max());

    public Task AddAsync(ChecklistItem item, CancellationToken ct = default) { Items.Add(item); return Task.CompletedTask; }
    public Task UpdateAsync(ChecklistItem item, CancellationToken ct = default)
    {
        var idx = Items.FindIndex(i => i.Id == item.Id);
        if (idx >= 0) Items[idx] = item;
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Guid itemId, CancellationToken ct = default) { Items.RemoveAll(i => i.Id == itemId); return Task.CompletedTask; }
    public Task CompactPositionsAsync(Guid cardId, int deletedPosition, CancellationToken ct = default)
    {
        var toShift = Items.Where(i => i.CardId == cardId && i.Position > deletedPosition).ToList();
        foreach (var item in toShift) item.Position -= 1;
        return Task.CompletedTask;
    }
    public Task UpdatePositionsAsync(IReadOnlyList<ChecklistItem> items, CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            var idx = Items.FindIndex(i => i.Id == item.Id);
            if (idx >= 0) Items[idx] = item;
        }
        return Task.CompletedTask;
    }
}

internal class InMemoryCardRepository : ICardRepository
{
    public List<Card> Cards { get; } = [];

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(Cards.FirstOrDefault(c => c.Id == cardId));

    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(Cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));

    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult(Cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber));

    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
    {
        var query = Cards.Where(c => c.ProjectId == projectId);
        if (filter.ColumnId.HasValue)
            query = query.Where(c => c.ColumnId == filter.ColumnId.Value);
        if (!filter.IncludeArchived)
            query = query.Where(c => c.ArchivedAt == null);
        if (filter.Type.HasValue)
            query = query.Where(c => c.Type == filter.Type.Value);
        return Task.FromResult<IReadOnlyList<Card>>(query.ToList());
    }

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult(Cards.Where(c => c.ProjectId == projectId).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());

    public Task AddAsync(Card card, CancellationToken ct = default) { Cards.Add(card); return Task.CompletedTask; }
    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = Cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0) Cards[idx] = card;
        return Task.CompletedTask;
    }
    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        foreach (var c in cards)
        {
            var idx = Cards.FindIndex(x => x.Id == c.Id);
            if (idx >= 0) Cards[idx] = c;
        }
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { Cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default) => Task.CompletedTask;
    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
        => Task.FromResult(Cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
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

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, User>>(Users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id));

    public Task<User?> FindByUsernameAsync(string username)
        => Task.FromResult(Users.FirstOrDefault(u => u.Username == username));

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;
    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);
    public Task CreateAsync(User user) { Users.Add(user); return Task.CompletedTask; }
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default) => Task.FromResult(Result.Success());
}