namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Chat;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

public class CardChatLinkServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    // ── In-memory fakes ─────────────────────────────────────────────────────────

    private sealed class FakeLinkRepo : ICardChatLinkRepository
    {
        public List<CardChatLink> Links { get; } = [];

        public Task<CardChatLink?> GetByIdAsync(Guid linkId, CancellationToken ct = default) =>
            Task.FromResult(Links.FirstOrDefault(l => l.Id == linkId));

        public Task<IReadOnlyList<CardChatLink>> GetByCardAsync(Guid cardId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CardChatLink>>(
                Links.Where(l => l.CardId == cardId && l.ArchivedAt == null)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToList()
            );

        public Task AddAsync(CardChatLink link, CancellationToken ct = default)
        {
            Links.Add(link);
            return Task.CompletedTask;
        }

        public Task ArchiveAsync(Guid linkId, CancellationToken ct = default)
        {
            var link = Links.FirstOrDefault(l => l.Id == linkId);
            if (link != null)
                link.ArchivedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCardRepo : ICardRepository
    {
        public Dictionary<Guid, Card> Cards { get; } = [];

        public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default) =>
            Task.FromResult(Cards.TryGetValue(cardId, out var c) ? c : null);

        public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(
            IReadOnlyList<Guid> cardIds,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Card>>(
                Cards.Where(kv => cardIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value)
            );

        public Task<Card?> GetByProjectAndNumberAsync(
            Guid projectId,
            int cardNumber,
            CancellationToken ct = default
        ) => Task.FromResult<Card?>(null);

        public Task<IReadOnlyList<Card>> ListByProjectAsync(
            Guid projectId,
            CardListFilter filter,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<Card>>([]);

        public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task AddAsync(Card card, CancellationToken ct = default)
        {
            Cards[card.Id] = card;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Card card, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid cardId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task CompactColumnPositionsAsync(
            Guid columnId,
            int exceptPosition,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<int> CountActiveChildrenAsync(Guid parentCardId, CancellationToken ct = default) =>
            Task.FromResult(0);
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public Dictionary<Guid, User> Users { get; } = [];

        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Users.TryGetValue(id, out var u) ? u : null);

        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyDictionary<Guid, User>>(
                Users.Where(kv => ids.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value)
            );

        public Task<User?> FindByUsernameAsync(string username) =>
            Task.FromResult<User?>(null);

        public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
            IReadOnlyList<string> usernames,
            string? searchTerm = null,
            int maxResults = 10,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<string, User>>(new Dictionary<string, User>());

        public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) =>
            Task.CompletedTask;

        public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);

        public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task CreateAsync(User user, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<User>> ListAsync(
            int skip,
            int take,
            string? search,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<User>>([]);

        public Task<int> CountAsync(string? search, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task UpdateAsync(User user, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeMemberRepo : IProjectMemberRepository
    {
        public bool DenyAccess { get; set; }

        public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ProjectMember?>(null);

        public Task<ProjectMember?> GetByProjectAndUserAsync(
            Guid projectId,
            Guid userId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<ProjectMember?>(
                DenyAccess
                    ? null
                    : new ProjectMember
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        UserId = userId,
                        Role = MemberRole.Owner,
                    }
            );

        public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
            Guid projectId,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ProjectMember>>([]);

        public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
            IEnumerable<Guid> projectIds,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
            IEnumerable<Guid> projectIds,
            Guid userId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(
                projectIds.ToDictionary(id => id, _ => MemberRole.Owner)
            );

        public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> HasAnyRoleAsync(
            Guid projectId,
            Guid userId,
            CancellationToken ct = default
        ) => Task.FromResult(!DenyAccess);
    }

    // ── SUT factory ───────────────────────────────────────────────────────────

    private static (
        CardChatLinkService service,
        FakeLinkRepo linkRepo,
        FakeCardRepo cardRepo,
        FakeUserRepo userRepo
    ) CreateSut()
    {
        var linkRepo = new FakeLinkRepo();
        var cardRepo = new FakeCardRepo();
        var userRepo = new FakeUserRepo();
        var memberRepo = new FakeMemberRepo();
        var service = new CardChatLinkService(linkRepo, cardRepo, userRepo, memberRepo);
        return (service, linkRepo, cardRepo, userRepo);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByCardAsync_ProjectMember_ReturnsLinks()
    {
        var (service, linkRepo, cardRepo, userRepo) = CreateSut();
        var projectId = NewId();
        var cardId = NewId();
        var ownerId = NewId();

        cardRepo.Cards[cardId] = new Card
        {
            Id = cardId,
            ProjectId = projectId,
            ColumnId = NewId(),
            Title = "Test Card",
        };

        userRepo.Users[ownerId] = User.Create(
            "session_owner", "Session", "Owner", "owner@test.com", "hash", id: ownerId
        );

        var link = new CardChatLink
        {
            Id = NewId(),
            CardId = cardId,
            ChatSessionId = NewId(),
            OwnerId = ownerId,
            Summary = "Discussed the API design for the new endpoint",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        };
        linkRepo.Links.Add(link);

        var userId = NewId();

        var result = await service.GetByCardAsync(cardId, userId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(link.Id, result.Value[0].Id);
        Assert.Equal("session_owner", result.Value[0].OwnerUsername);
        Assert.Equal(link.Summary, result.Value[0].Summary);
    }

    [Fact]
    public async Task GetByCardAsync_NonMember_ReturnsForbidden()
    {
        var (service, _, cardRepo, _) = CreateSut();
        var projectId = NewId();
        var cardId = NewId();

        cardRepo.Cards[cardId] = new Card
        {
            Id = cardId,
            ProjectId = projectId,
            ColumnId = NewId(),
            Title = "Test Card",
        };

        var memberRepo = new FakeMemberRepo { DenyAccess = true };
        var userRepo = new FakeUserRepo();
        var linkRepo = new FakeLinkRepo();
        var cardRepo2 = cardRepo;
        var denyService = new CardChatLinkService(linkRepo, cardRepo2, userRepo, memberRepo);

        var result = await denyService.GetByCardAsync(cardId, NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task GetByCardAsync_CardNotFound_ReturnsError()
    {
        var (service, _, _, _) = CreateSut();

        var result = await service.GetByCardAsync(NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_Owner_ArchivesLink()
    {
        var (service, linkRepo, _, _) = CreateSut();
        var ownerId = NewId();
        var link = new CardChatLink
        {
            Id = NewId(),
            CardId = NewId(),
            ChatSessionId = NewId(),
            OwnerId = ownerId,
            Summary = "Session summary",
        };
        linkRepo.Links.Add(link);

        var result = await service.ArchiveAsync(link.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(link.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_NonOwner_ReturnsForbidden()
    {
        var (service, linkRepo, _, _) = CreateSut();
        var ownerId = NewId();
        var otherId = NewId();
        var link = new CardChatLink
        {
            Id = NewId(),
            CardId = NewId(),
            ChatSessionId = NewId(),
            OwnerId = ownerId,
            Summary = "Session summary",
        };
        linkRepo.Links.Add(link);

        var result = await service.ArchiveAsync(link.Id, otherId);

        Assert.True(result.IsFailure);
        Assert.Equal("CHAT_SESSION_NOT_OWNER", result.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_LinkNotFound_ReturnsError()
    {
        var (service, _, _, _) = CreateSut();

        var result = await service.ArchiveAsync(NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotFound, result.Error.Code);
    }
}
