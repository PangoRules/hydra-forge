namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;

public class ChatSearchServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    private sealed class FakeSessionRepo : IChatSessionRepository
    {
        public List<ChatSession> Sessions { get; } = [];
        public List<(Guid ProjectId, Guid UserId)> Memberships { get; } = [];

        public Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

        public Task<ChatSession?> GetActiveByPanelAsync(
            Guid projectId,
            Guid? openCardId,
            Guid ownerId,
            CancellationToken ct = default
        ) => Task.FromResult<ChatSession?>(null);

        public Task<IReadOnlyList<ChatSession>> ListAsync(
            Guid ownerId,
            Guid? folderId,
            Guid? projectId,
            DateTime? before,
            Guid? beforeId,
            int limit,
            bool isAdmin = false,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
            ChatSessionScope scope = ChatSessionScope.Mine,
            IReadOnlySet<ChatSessionKind>? types = null,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task<int> CountAsync(
            Guid ownerId,
            Guid? folderId,
            Guid? projectId,
            bool isAdmin = false,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
            ChatSessionScope scope = ChatSessionScope.Mine,
            IReadOnlySet<ChatSessionKind>? types = null,
            CancellationToken ct = default
        ) => Task.FromResult(0);

        public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
            Guid ownerId,
            string query,
            Guid? projectId,
            int limit,
            bool isAdmin = false,
            ChatSessionScope scope = ChatSessionScope.Mine,
            CancellationToken ct = default
        )
        {
            if (string.IsNullOrWhiteSpace(query))
                return Task.FromResult<IReadOnlyList<ChatSession>>([]);

            var results = Sessions
                .Where(s =>
                    (
                        scope == ChatSessionScope.Participated
                            ? s.OwnerId == ownerId
                                || (
                                    s.ProjectId.HasValue
                                    && Memberships.Contains((s.ProjectId.Value, ownerId))
                                )
                            : s.OwnerId == ownerId
                    )
                    && s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && s.ArchivedAt == null
                    && (!projectId.HasValue || s.ProjectId == projectId)
                )
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<ChatSession>>(results);
        }

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatSession session, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeMessageRepo : IChatMessageRepository
    {
        private readonly FakeSessionRepo _sessionRepo;

        public List<ChatMessage> Messages { get; } = [];

        public FakeMessageRepo(FakeSessionRepo sessionRepo)
        {
            _sessionRepo = sessionRepo;
        }

        public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.Id == messageId));

        public Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
            Guid sessionId,
            DateTime? before,
            Guid? beforeId,
            int limit,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

        public Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
            Guid ownerId,
            string query,
            Guid? projectId,
            int limit,
            ChatSessionScope scope = ChatSessionScope.Mine,
            CancellationToken ct = default
        )
        {
            if (string.IsNullOrWhiteSpace(query))
                return Task.FromResult<IReadOnlyList<ChatMessage>>([]);

            var sessionIds = _sessionRepo
                .Sessions.Where(s =>
                    (
                        scope == ChatSessionScope.Participated
                            ? s.OwnerId == ownerId
                                || (
                                    s.ProjectId.HasValue
                                    && _sessionRepo.Memberships.Contains(
                                        (s.ProjectId.Value, ownerId)
                                    )
                                )
                            : s.OwnerId == ownerId
                    )
                    && s.ArchivedAt == null
                    && (!projectId.HasValue || s.ProjectId == projectId)
                )
                .Select(s => s.Id)
                .ToHashSet();

            var results = Messages
                .Where(m =>
                    sessionIds.Contains(m.SessionId)
                    && m.Content.Contains(query, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            return Task.FromResult<IReadOnlyList<ChatMessage>>(results);
        }

        public Task AddAsync(ChatMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteFromAsync(
            Guid sessionId,
            Guid messageId,
            CancellationToken ct = default
        ) => Task.FromResult(false);
    }

    private static (
        ChatSearchService service,
        FakeSessionRepo sessionRepo,
        FakeMessageRepo messageRepo
    ) CreateSut()
    {
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo(sessionRepo);
        var service = new ChatSearchService(sessionRepo, messageRepo);
        return (service, sessionRepo, messageRepo);
    }

    [Fact]
    public async Task SearchAsync_TitleMatch_ReturnsResultWithTitleMatchedOn()
    {
        var (service, sessionRepo, _) = CreateSut();
        var userId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = userId,
            Title = "Architecture discussion",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var results = await service.SearchAsync(userId, "Architecture");

        Assert.Single(results);
        Assert.Equal(session.Id, results[0].SessionId);
        Assert.Equal("Architecture discussion", results[0].SessionTitle);
        Assert.Equal("Title", results[0].MatchedOn);
        Assert.Null(results[0].Snippet);
    }

    [Fact]
    public async Task SearchAsync_ContentMatch_ReturnsResultWithSnippet()
    {
        var (service, sessionRepo, messageRepo) = CreateSut();
        var userId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = userId,
            Title = "Project chat",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = session.Id,
                Role = MessageRole.User,
                Content = "Tell me about the architecture of the system.",
            }
        );

        var results = await service.SearchAsync(userId, "architecture");

        Assert.Single(results);
        Assert.Equal(session.Id, results[0].SessionId);
        Assert.Equal("Project chat", results[0].SessionTitle);
        Assert.Equal("Content", results[0].MatchedOn);
        Assert.NotNull(results[0].Snippet);
        Assert.Contains("architecture", results[0].Snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_ProjectFilter_ReturnsOnlyMatchingProject()
    {
        var (service, sessionRepo, _) = CreateSut();
        var userId = NewId();
        var projectA = NewId();
        var projectB = NewId();

        var sessionA = new ChatSession
        {
            Id = NewId(),
            OwnerId = userId,
            ProjectId = projectA,
            Title = "Project A session",
            Status = ChatSessionStatus.Active,
        };
        var sessionB = new ChatSession
        {
            Id = NewId(),
            OwnerId = userId,
            ProjectId = projectB,
            Title = "Project B session",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(sessionA);
        sessionRepo.Sessions.Add(sessionB);

        var results = await service.SearchAsync(userId, "Project", projectId: projectA);

        Assert.Single(results);
        Assert.Equal(sessionA.Id, results[0].SessionId);
    }

    [Fact]
    public async Task SearchAsync_MoreThanMaxResults_CapsMergedListAtMaxResults()
    {
        var (service, sessionRepo, messageRepo) = CreateSut();
        var userId = NewId();

        for (var i = 0; i < 15; i++)
        {
            sessionRepo.Sessions.Add(
                new ChatSession
                {
                    Id = NewId(),
                    OwnerId = userId,
                    Title = $"Widget session {i}",
                    Status = ChatSessionStatus.Active,
                }
            );
        }

        for (var i = 0; i < 10; i++)
        {
            var session = new ChatSession
            {
                Id = NewId(),
                OwnerId = userId,
                Title = $"Unrelated session {i}",
                Status = ChatSessionStatus.Active,
            };
            sessionRepo.Sessions.Add(session);
            messageRepo.Messages.Add(
                new ChatMessage
                {
                    Id = NewId(),
                    SessionId = session.Id,
                    Role = MessageRole.User,
                    Content = "Talking about the widget rollout.",
                }
            );
        }

        var results = await service.SearchAsync(userId, "widget");

        Assert.Equal(20, results.Count);
        Assert.Equal(20, results.Select(r => r.SessionId).Distinct().Count());
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmptyList()
    {
        var (service, _, _) = CreateSut();
        var userId = NewId();

        var results = await service.SearchAsync(userId, "nonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_DefaultScope_ExcludesParticipatedOnlyTitleMatch()
    {
        var (service, sessionRepo, _) = CreateSut();
        var ownerId = NewId();
        var memberId = NewId();
        var projectId = NewId();

        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                ProjectId = projectId,
                Title = "Widget rollout plan",
                Status = ChatSessionStatus.Active,
            }
        );
        sessionRepo.Memberships.Add((projectId, memberId));

        var results = await service.SearchAsync(memberId, "Widget");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ScopeParticipated_IncludesProjectMemberTitleMatch()
    {
        var (service, sessionRepo, _) = CreateSut();
        var ownerId = NewId();
        var memberId = NewId();
        var projectId = NewId();

        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Title = "Widget rollout plan",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        sessionRepo.Memberships.Add((projectId, memberId));

        var results = await service.SearchAsync(
            memberId,
            "Widget",
            scope: ChatSessionScope.Participated
        );

        Assert.Single(results);
        Assert.Equal(session.Id, results[0].SessionId);
    }
}
