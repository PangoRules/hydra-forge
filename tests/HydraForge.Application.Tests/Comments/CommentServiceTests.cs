using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Comments;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Result = HydraForge.Domain.Common.Result;

namespace HydraForge.Application.Tests.Comments;

public class CommentServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_CreatesComment_AuthorAutoAddedAsWatcher()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });

        var result = await service.CreateAsync(new CreateCommentCommand(projectId, cardId, actorId, "Hello world"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello world", result.Value.Content);
        Assert.Equal(actorId, result.Value.AuthorId);
        Assert.Equal("author", result.Value.AuthorUsername);
        Assert.Empty(result.Value.MentionedUserIds);

        var watcher = await watcherRepo.GetByCardAndUserAsync(cardId, actorId);
        Assert.NotNull(watcher);
        Assert.Equal(cardId, watcher.CardId);
        Assert.Equal(actorId, watcher.UserId);
    }

    [Fact]
    public async Task CreateAsync_WithMention_ExtractsMentionedUser()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var mentionedId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = mentionedId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        userRepo.Users.Add(new User { Id = mentionedId, Username = "alice", Email = "b@b.com", PasswordHash = "x" });

        var result = await service.CreateAsync(new CreateCommentCommand(projectId, cardId, actorId, "Hey @alice check this"));

        Assert.True(result.IsSuccess);
        Assert.Contains(mentionedId, result.Value.MentionedUserIds);
    }

    [Fact]
    public async Task CreateAsync_MentionDisabledUser_IgnoresMention()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var disabledId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = disabledId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        userRepo.Users.Add(new User { Id = disabledId, Username = "ghost", Email = "g@g.com", PasswordHash = "x", IsDisabled = true });

        var result = await service.CreateAsync(new CreateCommentCommand(projectId, cardId, actorId, "Hey @ghost are you there?"));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.MentionedUserIds);
    }

    [Fact]
    public async Task CreateAsync_MentionNonMember_IgnoresMention()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var nonMemberId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        userRepo.Users.Add(new User { Id = nonMemberId, Username = "outsider", Email = "o@o.com", PasswordHash = "x" });

        var result = await service.CreateAsync(new CreateCommentCommand(projectId, cardId, actorId, "Hey @outsider"));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.MentionedUserIds);
    }

    [Fact]
    public async Task CreateAsync_AlreadyWatching_DoesNotDuplicateWatcher()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        watcherRepo.Watchers.Add(new CardWatcher { CardId = cardId, UserId = actorId });

        var result = await service.CreateAsync(new CreateCommentCommand(projectId, cardId, actorId, "Second comment"));

        Assert.True(result.IsSuccess);
        var watchers = await watcherRepo.ListByCardAsync(cardId);
        Assert.Single(watchers);
    }

    [Fact]
    public async Task CreateAsync_NonMember_ReturnsMembershipDenied()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var nonMemberId = NewId();

        var result = await service.CreateAsync(new CreateCommentCommand(projectId, cardId, nonMemberId, "Hello"));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContent_ReExtractsMentions()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var commentId = NewId();
        var mentionedId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = mentionedId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        userRepo.Users.Add(new User { Id = mentionedId, Username = "bob", Email = "b@b.com", PasswordHash = "x" });
        commentRepo.Comments.Add(new Comment { Id = commentId, CardId = cardId, AuthorId = actorId, Content = "Old" });

        var result = await service.UpdateAsync(new UpdateCommentCommand(projectId, cardId, commentId, actorId, "Updated @bob"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated @bob", result.Value.Content);
        Assert.Contains(mentionedId, result.Value.MentionedUserIds);
    }

    [Fact]
    public async Task UpdateAsync_ArchivedComment_ReturnsArchived()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var commentId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        commentRepo.Comments.Add(new Comment { Id = commentId, CardId = cardId, AuthorId = actorId, Content = "Old", ArchivedAt = DateTime.UtcNow });

        var result = await service.UpdateAsync(new UpdateCommentCommand(projectId, cardId, commentId, actorId, "New"));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Comments.Archived, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_SetsArchivedAt()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var commentId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        commentRepo.Comments.Add(new Comment { Id = commentId, CardId = cardId, AuthorId = actorId, Content = "To archive" });

        var result = await service.ArchiveAsync(new ArchiveCommentCommand(projectId, cardId, commentId, actorId));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_AlreadyArchived_ReturnsArchived()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var commentId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        commentRepo.Comments.Add(new Comment { Id = commentId, CardId = cardId, AuthorId = actorId, Content = "Already gone", ArchivedAt = DateTime.UtcNow });

        var result = await service.ArchiveAsync(new ArchiveCommentCommand(projectId, cardId, commentId, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Comments.Archived, result.Error.Code);
    }

    [Fact]
    public async Task ListAsync_ReturnsCommentsWithMentions()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var mentionedId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = mentionedId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        userRepo.Users.Add(new User { Id = mentionedId, Username = "alice", Email = "b@b.com", PasswordHash = "x" });
        commentRepo.Comments.Add(new Comment { Id = NewId(), CardId = cardId, AuthorId = actorId, Content = "Hi @alice" });

        var result = await service.ListAsync(projectId, cardId, actorId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Contains(mentionedId, result.Value[0].MentionedUserIds);
    }

    // ─── Audit tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WritesAuditLog()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });

        var result = await service.CreateAsync(new CreateCommentCommand(projectId, cardId, actorId, "Hello world"));

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Requests);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Comment", req.EntityType);
        Assert.Equal(result.Value.Id, req.EntityId);
        Assert.Equal("Created", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task UpdateAsync_WritesAuditLog()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var commentId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        commentRepo.Comments.Add(new Comment { Id = commentId, CardId = cardId, AuthorId = actorId, Content = "Old" });

        var result = await service.UpdateAsync(new UpdateCommentCommand(projectId, cardId, commentId, actorId, "Updated"));

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Requests);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Comment", req.EntityType);
        Assert.Equal(commentId, req.EntityId);
        Assert.Equal("Updated", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task ArchiveAsync_WritesAuditLog()
    {
        var (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new CommentService(commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var commentId = NewId();

        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), CardNumber = 1, Title = "Card" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        userRepo.Users.Add(new User { Id = actorId, Username = "author", Email = "a@a.com", PasswordHash = "x" });
        commentRepo.Comments.Add(new Comment { Id = commentId, CardId = cardId, AuthorId = actorId, Content = "To archive" });

        var result = await service.ArchiveAsync(new ArchiveCommentCommand(projectId, cardId, commentId, actorId));

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Requests);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Comment", req.EntityType);
        Assert.Equal(commentId, req.EntityId);
        Assert.Equal("Archived", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    private static (InMemoryCommentRepository, InMemoryCardWatcherRepository, InMemoryCardRepository, InMemoryProjectMemberRepository, InMemoryUserRepository, InMemoryAuditLogWriter, NullSnapshotRefresher, FakeProjectBoardEventPublisher) CreateMocks()
    {
        var commentRepo = new InMemoryCommentRepository();
        var watcherRepo = new InMemoryCardWatcherRepository();
        var cardRepo = new InMemoryCardRepository();
        var memberRepo = new InMemoryProjectMemberRepository();
        var userRepo = new InMemoryUserRepository();
        var auditWriter = new InMemoryAuditLogWriter();
        return (commentRepo, watcherRepo, cardRepo, memberRepo, userRepo, auditWriter, new NullSnapshotRefresher(), new FakeProjectBoardEventPublisher());
    }
}

internal class InMemoryCommentRepository : ICommentRepository
{
    public List<Comment> Comments { get; } = [];

    public Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken ct = default)
        => Task.FromResult(Comments.FirstOrDefault(c => c.Id == commentId));

    public Task<IReadOnlyList<Comment>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Comment>>(Comments.Where(c => c.CardId == cardId).OrderBy(c => c.CreatedAt).ToList());

    public Task AddAsync(Comment comment, CancellationToken ct = default) { Comments.Add(comment); return Task.CompletedTask; }
    public Task UpdateAsync(Comment comment, CancellationToken ct = default)
    {
        var idx = Comments.FindIndex(c => c.Id == comment.Id);
        if (idx >= 0) Comments[idx] = comment;
        return Task.CompletedTask;
    }
}

internal class InMemoryCardWatcherRepository : ICardWatcherRepository
{
    public List<CardWatcher> Watchers { get; } = [];

    public Task<CardWatcher?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Watchers.FirstOrDefault(w => w.CardId == cardId && w.UserId == userId));

    public Task<ILookup<Guid, CardWatcher>> ListByCardIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<ILookup<Guid, CardWatcher>>(Watchers.Where(w => cardIds.Contains(w.CardId)).ToLookup(w => w.CardId));

    public Task<IReadOnlyList<CardWatcher>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardWatcher>>(Watchers.Where(w => w.CardId == cardId).ToList());

    public Task AddAsync(CardWatcher watcher, CancellationToken ct = default) { Watchers.Add(watcher); return Task.CompletedTask; }
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { Watchers.RemoveAll(w => w.CardId == cardId && w.UserId == userId); return Task.CompletedTask; }
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

    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, User>>(Users.Where(u => usernames.Contains(u.Username, StringComparer.OrdinalIgnoreCase)).ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase));

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;
    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);
    public Task CreateAsync(User user) { Users.Add(user); return Task.CompletedTask; }
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public List<AuditLogRequest> Requests { get; } = [];

    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        return Task.FromResult(Result.Success());
    }

    public void Clear() => Requests.Clear();
}