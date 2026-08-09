namespace HydraForge.Application.Tests.Chat;

using System.Linq.Expressions;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Chat;
using HydraForge.Application.Projects;
using HydraForge.Application.Settings;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Result = HydraForge.Domain.Common.Result;

public class ChatSessionServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    // ── In-memory fakes ─────────────────────────────────────────────────────────

    private sealed class FakeSessionRepo : IChatSessionRepository
    {
        public List<ChatSession> Sessions { get; } = [];
        public List<CardChatLink> CapturedLinks { get; } = [];
        public int UpdateCallCount { get; private set; }
        private readonly FakeMemberRepo _memberRepo;

        public FakeSessionRepo(FakeMemberRepo memberRepo) => _memberRepo = memberRepo;

        public Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

        public Task<ChatSession?> GetActiveByPanelAsync(
            Guid projectId,
            Guid? openCardId,
            Guid ownerId,
            CancellationToken ct = default
        ) =>
            Task.FromResult(
                Sessions.FirstOrDefault(s =>
                    s.OwnerId == ownerId
                    && s.ProjectId == projectId
                    && s.OpenCardId == openCardId
                    && s.Status == ChatSessionStatus.Active
                )
            );

        public Task<IReadOnlyList<ChatSession>> ListAsync(
            Guid actorId,
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
        )
        {
            // isAdmin bypass means return all sessions (subject to other filters)
            // Participation = owner OR project member (mirrors EfChatSessionRepository.WhereParticipatedIn)
            var baseQuery = isAdmin
                ? Sessions.AsQueryable()
                : Sessions
                    .AsQueryable()
                    .Where(s =>
                        s.OwnerId == actorId
                        || (s.ProjectId != null && _memberRepo.IsMember(s.ProjectId.Value, actorId))
                    );

            var query = ApplyStatusFilter(baseQuery, statusFilter);
            if (folderId.HasValue)
                query = query.Where(s => s.FolderId == folderId.Value);
            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId.Value);
            if (before.HasValue)
                query = query.Where(s => s.CreatedAt < before.Value);
            if (types != null && types.Count > 0)
                query = query.Where(s =>
                    (types.Contains(ChatSessionKind.Normal) && s.ProjectId == null)
                    || (
                        types.Contains(ChatSessionKind.Project)
                        && s.ProjectId != null
                        && s.OpenCardId == null
                    )
                    || (types.Contains(ChatSessionKind.Card) && s.OpenCardId != null)
                );
            if (scope == ChatSessionScope.Participated)
            {
                query = query.Where(s =>
                    s.OwnerId == actorId
                    || (s.ProjectId != null && _memberRepo.IsMember(s.ProjectId.Value, actorId))
                );
            }

            return Task.FromResult<IReadOnlyList<ChatSession>>(
                query.OrderByDescending(s => s.UpdatedAt).Take(limit).ToList()
            );
        }

        public Task<int> CountAsync(
            Guid actorId,
            Guid? folderId,
            Guid? projectId,
            bool isAdmin = false,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
            ChatSessionScope scope = ChatSessionScope.Mine,
            IReadOnlySet<ChatSessionKind>? types = null,
            CancellationToken ct = default
        )
        {
            // isAdmin bypass means return all sessions (subject to other filters)
            // Participation = owner OR project member (mirrors EfChatSessionRepository.WhereParticipatedIn)
            var baseQuery = isAdmin
                ? Sessions.AsQueryable()
                : Sessions
                    .AsQueryable()
                    .Where(s =>
                        s.OwnerId == actorId
                        || (s.ProjectId != null && _memberRepo.IsMember(s.ProjectId.Value, actorId))
                    );

            var query = ApplyStatusFilter(baseQuery, statusFilter);
            if (folderId.HasValue)
                query = query.Where(s => s.FolderId == folderId.Value);
            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId.Value);
            if (types != null && types.Count > 0)
                query = query.Where(s =>
                    (types.Contains(ChatSessionKind.Normal) && s.ProjectId == null)
                    || (
                        types.Contains(ChatSessionKind.Project)
                        && s.ProjectId != null
                        && s.OpenCardId == null
                    )
                    || (types.Contains(ChatSessionKind.Card) && s.OpenCardId != null)
                );
            if (scope == ChatSessionScope.Participated)
            {
                query = query.Where(s =>
                    s.OwnerId == actorId
                    || (s.ProjectId != null && _memberRepo.IsMember(s.ProjectId.Value, actorId))
                );
            }

            return Task.FromResult(query.Count());
        }

        private static IQueryable<ChatSession> ApplyStatusFilter(
            IQueryable<ChatSession> query,
            ChatSessionStatusFilter statusFilter
        ) =>
            statusFilter switch
            {
                ChatSessionStatusFilter.Active => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Active
                ),
                ChatSessionStatusFilter.Closed => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Closed
                ),
                ChatSessionStatusFilter.Archived => query.Where(s => s.ArchivedAt != null),
                _ => query.Where(s => s.ArchivedAt == null),
            };

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatSession session, CancellationToken ct = default)
        {
            UpdateCallCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
            Guid actorId,
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

            // isAdmin bypass means return all sessions (subject to other filters)
            // Participation = owner OR project member (mirrors EfChatSessionRepository.WhereParticipatedIn)
            var baseQuery = isAdmin
                ? Sessions.AsQueryable()
                : Sessions
                    .AsQueryable()
                    .Where(s =>
                        s.OwnerId == actorId
                        || (s.ProjectId != null && _memberRepo.IsMember(s.ProjectId.Value, actorId))
                    );

            var queryResults = baseQuery.Where(s =>
                s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                && s.ArchivedAt == null
                && (!projectId.HasValue || s.ProjectId == projectId)
            );

            if (scope == ChatSessionScope.Participated)
            {
                queryResults = queryResults.Where(s =>
                    s.OwnerId == actorId
                    || (s.ProjectId != null && _memberRepo.IsMember(s.ProjectId.Value, actorId))
                );
            }

            return Task.FromResult<IReadOnlyList<ChatSession>>(
                queryResults.OrderByDescending(s => s.UpdatedAt).Take(limit).ToList()
            );
        }

        public Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default)
        {
            CapturedLinks.Add(link);
            return Task.CompletedTask;
        }

        public Task<CardChatLink?> FindCardChatLinkAsync(
            Guid cardId,
            Guid chatSessionId,
            CancellationToken ct = default
        ) =>
            Task.FromResult(
                CapturedLinks.FirstOrDefault(l =>
                    l.CardId == cardId && l.ChatSessionId == chatSessionId
                )
            );

        public Task UpdateCardChatLinkSummaryAsync(
            Guid linkId,
            string summary,
            CancellationToken ct = default
        )
        {
            var link = CapturedLinks.FirstOrDefault(l => l.Id == linkId);
            if (link != null)
                link.Summary = summary;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMessageRepo : IChatMessageRepository
    {
        public List<ChatMessage> Messages { get; } = [];

        public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.Id == messageId));

        public Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
            Guid sessionId,
            DateTime? before,
            Guid? beforeId,
            int limit,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<ChatMessage>>(
                Messages.Where(m => m.SessionId == sessionId).ToList()
            );

        public Task AddAsync(ChatMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
            Guid ownerId,
            string query,
            Guid? projectId,
            int limit,
            bool isAdmin = false,
            ChatSessionScope scope = ChatSessionScope.Mine,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

        public Task<bool> DeleteFromAsync(
            Guid sessionId,
            Guid messageId,
            CancellationToken ct = default
        ) => Task.FromResult(false);
    }

    private sealed class FakeSessionDocRepo : IChatSessionDocumentRepository
    {
        public List<ChatSessionDocument> SessionDocs { get; } = [];

        public Task<IReadOnlyList<ChatSessionDocument>> GetBySessionAsync(
            Guid sessionId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<ChatSessionDocument>>(
                SessionDocs.Where(sd => sd.SessionId == sessionId).ToList()
            );

        public Task AddAsync(ChatSessionDocument sessionDocument, CancellationToken ct = default)
        {
            SessionDocs.Add(sessionDocument);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid sessionId, Guid documentId, CancellationToken ct = default)
        {
            SessionDocs.RemoveAll(sd => sd.SessionId == sessionId && sd.DocumentId == documentId);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(
            Guid sessionId,
            Guid documentId,
            CancellationToken ct = default
        ) =>
            Task.FromResult(
                SessionDocs.Any(sd => sd.SessionId == sessionId && sd.DocumentId == documentId)
            );
    }

    private sealed class FakeCardRepo : ICardRepository
    {
        private readonly Card? _card;
        public Dictionary<Guid, Card> Cards { get; } = [];

        public FakeCardRepo(Card? card = null) => _card = card;

        public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default) =>
            Task.FromResult(Cards.TryGetValue(cardId, out var c) ? c : _card);

        public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(
            IReadOnlyList<Guid> cardIds,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Card>>(
                _card != null && cardIds.Contains(_card.Id)
                    ? new Dictionary<Guid, Card> { [_card.Id] = _card }
                    : new Dictionary<Guid, Card>()
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

        public Task AddAsync(Card card, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Card card, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid cardId, CancellationToken ct = default) => Task.CompletedTask;

        public Task CompactColumnPositionsAsync(
            Guid columnId,
            int exceptPosition,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<int> CountActiveChildrenAsync(
            Guid parentCardId,
            CancellationToken ct = default
        ) => Task.FromResult(0);
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public Dictionary<Guid, User> Users { get; } = [];

        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Users.TryGetValue(id, out var u) ? u : null);

        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<Guid, User>>(new Dictionary<Guid, User>());

        public Task<User?> FindByUsernameAsync(string username) => Task.FromResult<User?>(null);

        public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
            IReadOnlyList<string> usernames,
            string? searchTerm = null,
            int maxResults = 10,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<string, User>>(new Dictionary<string, User>());

        public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

        public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);

        public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.TryGetValue(userId, out var u) && u.IsAdmin);

        public Task CreateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<User>> ListAsync(
            int skip,
            int take,
            string? search,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<User>>([]);

        public Task<int> CountAsync(string? search, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeMemberRepo : IProjectMemberRepository
    {
        public bool DenyAccess { get; set; }

        // (projectId, userId) -> membership — mirrors real DB state.
        // Instance field, not static: each test gets its own FakeMemberRepo via
        // CreateSut(), so this must not leak membership state across tests.
        private readonly Dictionary<(Guid projectId, Guid userId), ProjectMember> _memberships = [];

        public void AddMembership(Guid projectId, Guid userId, MemberRole role)
        {
            _memberships[(projectId, userId)] = new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                Role = role,
            };
        }

        public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ProjectMember?>(null);

        public Task<ProjectMember?> GetByProjectAndUserAsync(
            Guid projectId,
            Guid userId,
            CancellationToken ct = default
        )
        {
            if (DenyAccess)
                return Task.FromResult<ProjectMember?>(null);
            if (_memberships.TryGetValue((projectId, userId), out var member))
                return Task.FromResult<ProjectMember?>(member);
            return Task.FromResult<ProjectMember?>(
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    UserId = userId,
                    Role = MemberRole.Member,
                }
            );
        }

        public bool IsMember(Guid projectId, Guid userId) =>
            _memberships.ContainsKey((projectId, userId));

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
        ) => Task.FromResult(true);
    }

    private sealed class FakePersonalityRepo : IAgentPersonalityRepository
    {
        public Task<AgentPersonality?> GetByIdAsync(
            Guid personalityId,
            CancellationToken ct = default
        ) => Task.FromResult<AgentPersonality?>(null);

        public Task<IReadOnlyList<AgentPersonality>> ListByUserAsync(
            Guid userId,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<AgentPersonality>>([]);

        public Task<AgentPersonality?> GetDefaultAsync(
            Guid userId,
            CancellationToken ct = default
        ) => Task.FromResult<AgentPersonality?>(null);

        public Task AddAsync(AgentPersonality personality, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(AgentPersonality personality, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task ArchiveAsync(Guid personalityId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SetDefaultAsync(
            Guid personalityId,
            Guid userId,
            CancellationToken ct = default
        ) => Task.CompletedTask;
    }

    private sealed class FakeDocumentRepo : IDocumentRepository
    {
        public Dictionary<Guid, Document> Documents { get; } = [];

        public Task<Document?> GetByIdAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult(Documents.TryGetValue(documentId, out var d) ? d : null);

        public Task<IReadOnlyList<Document>> ListByUserAsync(
            Guid userId,
            string? q = null,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<Document>>([]);

        public Task AddAsync(Document document, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task ArchiveAsync(Guid documentId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeBackgroundTaskQueue : IBackgroundTaskQueue
    {
        public List<Func<CancellationToken, Task>> CapturedWorkItems { get; } = [];
        public List<Func<CancellationToken, Task>> CapturedJobDelegates { get; } = [];
        public int EnqueueCount { get; private set; }
        private object? _closeSessionJobInstance;

        public void SetCloseSessionJobInstance(object job) => _closeSessionJobInstance = job;

        public Task EnqueueAsync(
            Func<CancellationToken, Task> workItem,
            CancellationToken ct = default
        )
        {
            CapturedWorkItems.Add(workItem);
            return Task.CompletedTask;
        }

        public Task EnqueueJobAsync<TJob>(Expression<Func<TJob, Task>> methodCall)
        {
            EnqueueCount++;
            var func = methodCall.Compile();
            if (_closeSessionJobInstance is TJob job)
            {
                Func<CancellationToken, Task> del = async ct =>
                {
                    await func(job);
                };
                CapturedJobDelegates.Add(del);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSummaryGenerator : IChatSummaryGenerator
    {
        public Func<
            Guid,
            CancellationToken,
            Task<Result<string>>
        >? GenerateSummaryImpl { get; set; }

        public Task<Result<string>> GenerateSummaryAsync(
            Guid sessionId,
            CancellationToken ct = default
        )
        {
            if (GenerateSummaryImpl != null)
                return GenerateSummaryImpl(sessionId, ct);
            return Task.FromResult(Result<string>.Success("Test summary"));
        }
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SystemSettings? Settings { get; set; }

        public Task<SystemSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(Settings ?? new SystemSettings());

        public void Invalidate() { }
    }

    // ── SUT factory ───────────────────────────────────────────────────────────

    private static (
        ChatSessionService service,
        FakeSessionRepo sessionRepo,
        FakeMessageRepo messageRepo,
        FakeCardRepo cardRepo,
        FakeUserRepo userRepo,
        FakeMemberRepo memberRepo,
        FakePersonalityRepo personalityRepo,
        FakeDocumentRepo documentRepo,
        FakeSummaryGenerator summaryGenerator,
        FakeBackgroundTaskQueue backgroundTaskQueue,
        FakeSessionDocRepo sessionDocRepo,
        FakeSettingsProvider settingsProvider
    ) CreateSut()
    {
        var memberRepo = new FakeMemberRepo();
        var sessionRepo = new FakeSessionRepo(memberRepo);
        var messageRepo = new FakeMessageRepo();
        var sessionDocRepo = new FakeSessionDocRepo();
        var cardRepo = new FakeCardRepo();
        var userRepo = new FakeUserRepo();
        var personalityRepo = new FakePersonalityRepo();
        var documentRepo = new FakeDocumentRepo();
        var summaryGenerator = new FakeSummaryGenerator();
        var backgroundTaskQueue = new FakeBackgroundTaskQueue();
        var settingsProvider = new FakeSettingsProvider();
        var logger = Substitute.For<ILogger<ChatSessionService>>();

        var service = new ChatSessionService(
            sessionRepo,
            messageRepo,
            sessionDocRepo,
            cardRepo,
            userRepo,
            memberRepo,
            personalityRepo,
            documentRepo,
            summaryGenerator,
            backgroundTaskQueue,
            settingsProvider,
            logger
        );

        // CloseSessionJob takes IChatSessionService directly — mirrors production DI wiring
        // (only IChatSessionService is registered, not the concrete ChatSessionService type).
        var closeSessionJob = new CloseSessionJob(service);
        backgroundTaskQueue.SetCloseSessionJobInstance(closeSessionJob);

        return (
            service,
            sessionRepo,
            messageRepo,
            cardRepo,
            userRepo,
            memberRepo,
            personalityRepo,
            documentRepo,
            summaryGenerator,
            backgroundTaskQueue,
            sessionDocRepo,
            settingsProvider
        );
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsIdentitySystemMessage()
    {
        var (service, _, messageRepo, _, _, _, _, _, _, _, _, settingsProvider) = CreateSut();
        settingsProvider.Settings = new SystemSettings { AiIdentityPrompt = null };
        var actorId = NewId();

        var result = await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "test",
                FolderId: null,
                ProjectId: null,
                OpenCardId: null,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: null,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            actorId
        );

        Assert.True(result.IsSuccess);
        var systemMessage = messageRepo.Messages.FirstOrDefault(m => m.Role == MessageRole.System);
        Assert.NotNull(systemMessage);
        Assert.Contains("HydraForge", systemMessage!.Content);
    }

    [Fact]
    public async Task CreateAsync_F6_ImplicitClose_EnqueuesBackgroundJob()
    {
        var (service, sessionRepo, _, cardRepo, _, _, _, _, _, backgroundTaskQueue, _, _) =
            CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();

        // Existing active panel session
        var existing = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            OpenCardId = cardId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(existing);

        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };

        var result = await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "New Chat",
                FolderId: null,
                ProjectId: projectId,
                OpenCardId: cardId,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: null,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            ownerId
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(1, backgroundTaskQueue.EnqueueCount);
        Assert.Equal(ChatSessionStatus.Active, existing.Status); // Not closed yet — only enqueued
        Assert.Equal(2, sessionRepo.Sessions.Count);
    }

    [Fact]
    public async Task CreateAsync_F6_ImplicitClose_JobClosesOldSession()
    {
        var (
            service,
            sessionRepo,
            _,
            cardRepo,
            _,
            _,
            _,
            _,
            summaryGenerator,
            backgroundTaskQueue,
            _,
            _
        ) = CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();

        var existing = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            OpenCardId = cardId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(existing);

        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };
        summaryGenerator.GenerateSummaryImpl = (_, _) =>
            Task.FromResult(Result<string>.Success("Test summary"));

        await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "New Chat",
                FolderId: null,
                ProjectId: projectId,
                OpenCardId: cardId,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: null,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            ownerId
        );

        // Explicitly invoke the captured work item to verify it closes the old session.
        Assert.Single(backgroundTaskQueue.CapturedJobDelegates);
        var workItem = backgroundTaskQueue.CapturedJobDelegates[0];
        await workItem(CancellationToken.None);

        Assert.Equal(ChatSessionStatus.Closed, existing.Status);
    }

    [Fact]
    public async Task CloseAsync_Idempotent_ReturnsSameSessionTwice()
    {
        var (service, sessionRepo, messageRepo, _, _, _, _, _, summaryGenerator, _, _, _) =
            CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Title = "Test Session",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = session.Id,
                Role = MessageRole.User,
                Content = "Hello",
            }
        );

        summaryGenerator.GenerateSummaryImpl = (_, _) =>
            Task.FromResult(Result<string>.Success("Summary"));

        var result1 = await service.CloseAsync(session.Id, ownerId);
        var result2 = await service.CloseAsync(session.Id, ownerId);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.Value.Id, result2.Value.Id);
        Assert.Equal(ChatSessionStatus.Closed, result1.Value.Status);
        Assert.Equal(ChatSessionStatus.Closed, result2.Value.Status);
    }

    [Fact]
    public async Task CloseAsync_EmptySession_NoSummaryNoCardChatLink()
    {
        var (service, sessionRepo, messageRepo, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Title = "Empty Session",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.CloseAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatSessionStatus.Closed, result.Value.Status);
        Assert.Null(result.Value.Summary);
        Assert.Empty(sessionRepo.CapturedLinks);
        Assert.Empty(messageRepo.Messages);
    }

    [Fact]
    public async Task UpdateAsync_ClosedSession_ReturnsSessionClosedError()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Title = "Old Title",
            Status = ChatSessionStatus.Closed,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.UpdateAsync(
            session.Id,
            new UpdateChatSessionRequest(
                Title: "New Title",
                FolderId: null,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            ownerId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionClosed, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_Fork_CreatesIndependentSessionOwnedByCaller()
    {
        var (service, sessionRepo, messageRepo, _, _, _, _, _, summaryGenerator, _, _, _) =
            CreateSut();
        var ownerId = NewId();
        var callerId = NewId();
        var projectId = NewId();

        // Source session: shared project chat
        var source = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
            Title = "Source Session",
        };
        sessionRepo.Sessions.Add(source);
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = source.Id,
                Role = MessageRole.User,
                Content = "Hello",
            }
        );

        summaryGenerator.GenerateSummaryImpl = (_, _) =>
            Task.FromResult(Result<string>.Success("Forked summary"));

        var result = await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "My Fork",
                FolderId: null,
                ProjectId: null,
                OpenCardId: null,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: source.Id,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            callerId
        );

        Assert.True(result.IsSuccess);
        var forked = result.Value;
        var forkedSession = sessionRepo.Sessions.First(s => s.Id == forked.Id);
        Assert.Equal(callerId, forkedSession.OwnerId);
        Assert.Equal("My Fork", forked.Title);
        Assert.Null(forked.OpenCardId);
        Assert.Equal(ChatSessionStatus.Active, forked.Status);

        // Verify pre-populated summary message was added
        var summaryMessages = messageRepo
            .Messages.Where(m => m.SessionId == forked.Id && m.Role == MessageRole.Assistant)
            .ToList();
        Assert.Single(summaryMessages);
        Assert.Contains("Forked summary", summaryMessages[0].Content);
    }

    [Fact]
    public async Task GetPermissionAsync_Granted_WhenActiveProjectSession()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = NewId(),
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.GetPermissionAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Granted);
    }

    [Fact]
    public async Task GetPermissionAsync_Denied_WhenPersonalOrClosed()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();

        // Personal session (no project) — denied
        var personal = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = null,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(personal);

        var resultPersonal = await service.GetPermissionAsync(personal.Id, ownerId);
        Assert.True(resultPersonal.IsSuccess);
        Assert.False(resultPersonal.Value.Granted);

        // Closed project session — denied
        var closed = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = NewId(),
            Status = ChatSessionStatus.Closed,
        };
        sessionRepo.Sessions.Add(closed);

        var resultClosed = await service.GetPermissionAsync(closed.Id, ownerId);
        Assert.True(resultClosed.IsSuccess);
        Assert.False(resultClosed.Value.Granted);
    }

    [Fact]
    public async Task CloseAsync_WithMessages_CreatesCardChatLink()
    {
        var (service, sessionRepo, messageRepo, _, _, _, _, _, summaryGenerator, _, _, _) =
            CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            OpenCardId = cardId,
            Title = "Panel Session",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = session.Id,
                Role = MessageRole.User,
                Content = "Hello",
            }
        );

        summaryGenerator.GenerateSummaryImpl = (_, _) =>
            Task.FromResult(Result<string>.Success("Panel summary"));

        var result = await service.CloseAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Single(sessionRepo.CapturedLinks);
        var link = sessionRepo.CapturedLinks[0];
        Assert.Equal(cardId, link.CardId);
        Assert.Equal(session.Id, link.ChatSessionId);
        Assert.Equal(ownerId, link.OwnerId);
        Assert.Equal("Panel summary", link.Summary);
    }

    [Fact]
    public async Task CloseAsync_SummaryGenerationFails_ClosesWithNullSummaryAndFallbackCardChatLink()
    {
        var (service, sessionRepo, messageRepo, _, _, _, _, _, summaryGenerator, _, _, _) =
            CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            OpenCardId = cardId,
            Title = "Panel Session",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = session.Id,
                Role = MessageRole.User,
                Content = "Hello",
            }
        );

        summaryGenerator.GenerateSummaryImpl = (_, _) =>
            Task.FromResult(
                Result<string>.Failure(
                    new Error(DomainErrorCodes.Chat.SummaryFailed, "LLM call failed.")
                )
            );

        var result = await service.CloseAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Summary);
        Assert.Equal(ChatSessionStatus.Closed, session.Status);
        Assert.Single(sessionRepo.CapturedLinks);
        var link = sessionRepo.CapturedLinks[0];
        Assert.Equal(cardId, link.CardId);
        Assert.Equal("Chat closed (summary unavailable)", link.Summary);
    }

    [Fact]
    public async Task CreateAsync_WithPreferredModelConfigId_PersistsIt()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var actorId = NewId();
        var modelConfigId = Guid.NewGuid();

        var result = await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "test",
                FolderId: null,
                ProjectId: null,
                OpenCardId: null,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: null,
                PreferredModelConfigId: modelConfigId,
                PreferredEffort: "medium"
            ),
            actorId
        );

        Assert.True(result.IsSuccess);
        var saved = sessionRepo.Sessions.Single();
        Assert.Equal(modelConfigId, saved.PreferredModelConfigId);
        Assert.Equal("medium", saved.PreferredEffort);
    }

    [Fact]
    public async Task ArchiveAsync_NotOwner_ReturnsSessionNotOwner()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var callerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.ArchiveAsync(session.Id, callerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task DetachDocumentAsync_NotOwner_ReturnsSessionNotOwner()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var callerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.DetachDocumentAsync(session.Id, NewId(), callerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_NoProjectNoFork_CreatesPersonalSession()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();

        var result = await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "Personal Chat",
                FolderId: null,
                ProjectId: null,
                OpenCardId: null,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: null,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            ownerId
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatSessionStatus.Active, result.Value.Status);
        Assert.Single(sessionRepo.Sessions);
    }

    [Fact]
    public async Task CreateAsync_MembershipDenied_ReturnsError()
    {
        var (service, _, _, _, _, memberRepo, _, _, _, _, _, _) = CreateSut();
        memberRepo.DenyAccess = true;

        var result = await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "Chat",
                FolderId: null,
                ProjectId: NewId(),
                OpenCardId: null,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: null,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_OpenCardWithoutProject_ReturnsError()
    {
        var (service, _, _, _, _, _, _, _, _, _, _, _) = CreateSut();

        var result = await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "Chat",
                FolderId: null,
                ProjectId: null,
                OpenCardId: NewId(),
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: null,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.CardNotInProject, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_CardInDifferentProject_ReturnsError()
    {
        var (service, _, _, cardRepo, _, _, _, _, _, _, _, _) = CreateSut();
        var projectId = NewId();
        var otherProjectId = NewId();
        var cardId = NewId();
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = otherProjectId };

        var result = await service.CreateAsync(
            new CreateChatSessionRequest(
                Title: "Chat",
                FolderId: null,
                ProjectId: projectId,
                OpenCardId: cardId,
                PersonalityId: null,
                AiEditMode: null,
                SearchAllMyDocs: false,
                ForkedFromSessionId: null,
                PreferredModelConfigId: null,
                PreferredEffort: null
            ),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.CardNotInProject, result.Error.Code);
    }

    [Fact]
    public async Task GetAsync_Owner_ReturnsDetailWithMessages()
    {
        var (service, sessionRepo, messageRepo, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Title = "Chat",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = session.Id,
                Role = MessageRole.User,
                Content = "Hi",
            }
        );

        var result = await service.GetAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(session.Id, result.Value.Id);
        Assert.Single(result.Value.Messages);
    }

    [Fact]
    public async Task GetAsync_NotOwnerNotShared_ReturnsAccessDenied()
    {
        var (service, sessionRepo, _, _, _, memberRepo, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            IsShared = false,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.GetAsync(session.Id, NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyCallersNonArchivedSessions()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var otherOwnerId = NewId();
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                Status = ChatSessionStatus.Active,
            }
        );
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                ArchivedAt = DateTime.UtcNow,
                Status = ChatSessionStatus.Active,
            }
        );
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = otherOwnerId,
                Status = ChatSessionStatus.Active,
            }
        );

        var result = await service.ListAsync(
            ownerId,
            folderId: null,
            projectId: null,
            before: null,
            beforeId: null,
            limit: 50
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task ListAsync_TotalCountIsRealTotalNotPageSize()
    {
        // Regression: TotalCount was set to dtos.Count (page size), not the real
        // total — the client used sessions.length < totalCount to decide whether
        // to fetch the next page, so a page-size TotalCount made hasMore false
        // after page 1 and every session beyond the first page unreachable.
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        for (var i = 0; i < 25; i++)
        {
            sessionRepo.Sessions.Add(
                new ChatSession
                {
                    Id = NewId(),
                    OwnerId = ownerId,
                    Status = ChatSessionStatus.Active,
                    UpdatedAt = DateTime.UtcNow.AddSeconds(-i),
                }
            );
        }

        var result = await service.ListAsync(
            ownerId,
            folderId: null,
            projectId: null,
            before: null,
            beforeId: null,
            limit: 20
        );

        Assert.True(result.IsSuccess);
        // Page has 20 items (the limit), but TotalCount must be 25 — the real
        // total — so the client knows there's another page to fetch.
        Assert.Equal(20, result.Value.Items.Count);
        Assert.Equal(25, result.Value.TotalCount);
    }

    [Fact]
    public async Task AttachDocumentAsync_OwnedDocument_AttachesSuccessfully()
    {
        var (service, sessionRepo, _, _, _, _, _, documentRepo, _, _, sessionDocRepo, _) =
            CreateSut();
        var ownerId = NewId();
        var documentId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        documentRepo.Documents[documentId] = new Document
        {
            Id = documentId,
            UserId = ownerId,
            Title = "Doc",
        };

        var result = await service.AttachDocumentAsync(session.Id, documentId, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Single(sessionDocRepo.SessionDocs);
        Assert.Equal(documentId, sessionDocRepo.SessionDocs[0].DocumentId);
    }

    [Fact]
    public async Task AttachDocumentAsync_NotOwnedDocument_ReturnsError()
    {
        var (service, sessionRepo, _, _, _, _, _, documentRepo, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var documentId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        documentRepo.Documents[documentId] = new Document
        {
            Id = documentId,
            UserId = NewId(),
            Title = "Doc",
        };

        var result = await service.AttachDocumentAsync(session.Id, documentId, ownerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.DocumentNotOwned, result.Error.Code);
    }

    [Fact]
    public async Task AttachDocumentAsync_AlreadyAttached_ReturnsError()
    {
        var (service, sessionRepo, _, _, _, _, _, documentRepo, _, _, sessionDocRepo, _) =
            CreateSut();
        var ownerId = NewId();
        var documentId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        documentRepo.Documents[documentId] = new Document
        {
            Id = documentId,
            UserId = ownerId,
            Title = "Doc",
        };
        sessionDocRepo.SessionDocs.Add(
            new ChatSessionDocument
            {
                Id = NewId(),
                SessionId = session.Id,
                DocumentId = documentId,
                AddedByUserId = ownerId,
            }
        );

        var result = await service.AttachDocumentAsync(session.Id, documentId, ownerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.DocumentAlreadyAttached, result.Error.Code);
    }

    [Fact]
    public async Task ListDocumentsAsync_ReturnsAttachedNonArchivedDocuments()
    {
        var (service, sessionRepo, _, _, _, _, _, documentRepo, _, _, sessionDocRepo, _) =
            CreateSut();
        var ownerId = NewId();
        var attachedId = NewId();
        var archivedId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        documentRepo.Documents[attachedId] = new Document
        {
            Id = attachedId,
            UserId = ownerId,
            Title = "Kept",
        };
        documentRepo.Documents[archivedId] = new Document
        {
            Id = archivedId,
            UserId = ownerId,
            Title = "Archived",
            ArchivedAt = DateTime.UtcNow,
        };
        sessionDocRepo.SessionDocs.Add(
            new ChatSessionDocument
            {
                Id = NewId(),
                SessionId = session.Id,
                DocumentId = attachedId,
                AddedByUserId = ownerId,
            }
        );
        sessionDocRepo.SessionDocs.Add(
            new ChatSessionDocument
            {
                Id = NewId(),
                SessionId = session.Id,
                DocumentId = archivedId,
                AddedByUserId = ownerId,
            }
        );

        var result = await service.ListDocumentsAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(attachedId, result.Value[0].DocumentId);
    }

    [Fact]
    public async Task ListAsync_StatusFilterArchived_ReturnsOnlyArchivedRegardlessOfStatus()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                Status = ChatSessionStatus.Active,
                ArchivedAt = DateTime.UtcNow,
            }
        );
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                Status = ChatSessionStatus.Closed,
                ArchivedAt = DateTime.UtcNow,
            }
        );
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                Status = ChatSessionStatus.Active,
                ArchivedAt = null,
            }
        );

        var result = await service.ListAsync(
            ownerId,
            folderId: null,
            projectId: null,
            before: null,
            beforeId: null,
            limit: 50,
            statusFilter: ChatSessionStatusFilter.Archived
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task ListAsync_StatusFilterClosed_ExcludesActiveAndArchived()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                Status = ChatSessionStatus.Closed,
                ArchivedAt = null,
            }
        );
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                Status = ChatSessionStatus.Active,
                ArchivedAt = null,
            }
        );
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = NewId(),
                OwnerId = ownerId,
                Status = ChatSessionStatus.Closed,
                ArchivedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ListAsync(
            ownerId,
            folderId: null,
            projectId: null,
            before: null,
            beforeId: null,
            limit: 50,
            statusFilter: ChatSessionStatusFilter.Closed
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task ReopenAsync_ClosedSession_SetsStatusActiveAndResetsAiEditMode()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Closed,
            AiEditMode = AiEditMode.Blanket,
            ClosedAt = DateTime.UtcNow,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.ReopenAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatSessionStatus.Active, result.Value.Status);
        Assert.Equal(AiEditMode.PerMutation, result.Value.AiEditMode);
    }

    [Fact]
    public async Task ReopenAsync_ArchivedAndClosedSession_ClearsArchivedAtInOneCall()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Closed,
            ArchivedAt = DateTime.UtcNow,
            ClosedAt = DateTime.UtcNow,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.ReopenAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ArchivedAt);
        Assert.Equal(ChatSessionStatus.Active, result.Value.Status);
    }

    [Fact]
    public async Task ReopenAsync_NotOwner_ReturnsFailure()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var callerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Closed,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.ReopenAsync(session.Id, callerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task ReopenAsync_SessionNotFound_ReturnsFailure()
    {
        var (service, _, _, _, _, _, _, _, _, _, _, _) = CreateSut();

        var result = await service.ReopenAsync(NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotFound, result.Error.Code);
    }

    // ── Participation-aware ListAsync tests ─────────────────────────────────────

    [Fact]
    public async Task ListAsync_ParticipationIn_ProjectSessionVisibleToMember()
    {
        var (service, sessionRepo, _, _, userRepo, memberRepo, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var memberId = NewId();
        var projectId = NewId();

        userRepo.Users[ownerId] = User.Create(
            "owner",
            "Owner",
            "User",
            "owner@test.com",
            "hash",
            isAdmin: false,
            id: ownerId
        );
        userRepo.Users[memberId] = User.Create(
            "member",
            "Member",
            "User",
            "member@test.com",
            "hash",
            isAdmin: false,
            id: memberId
        );
        memberRepo.AddMembership(projectId, memberId, MemberRole.Member);

        var projectSession = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(projectSession);

        var result = await service.ListAsync(
            memberId,
            folderId: null,
            projectId: projectId,
            before: null,
            beforeId: null,
            limit: 50
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(projectSession.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_NonMemberExcluded()
    {
        var (service, sessionRepo, _, _, userRepo, memberRepo, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var nonMemberId = NewId();
        var projectId = NewId();

        userRepo.Users[ownerId] = User.Create(
            "owner",
            "Owner",
            "User",
            "owner@test.com",
            "hash",
            isAdmin: false,
            id: ownerId
        );
        userRepo.Users[nonMemberId] = User.Create(
            "nonmember",
            "Non",
            "Member",
            "nonmember@test.com",
            "hash",
            isAdmin: false,
            id: nonMemberId
        );
        // DenyAccess = false (default) so MembershipGuard returns false for non-member

        var projectSession = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(projectSession);

        var result = await service.ListAsync(
            nonMemberId,
            folderId: null,
            projectId: projectId,
            before: null,
            beforeId: null,
            limit: 50
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalCount);
    }

    [Fact]
    public async Task ListAsync_AdminSeesAll()
    {
        var (service, sessionRepo, _, _, userRepo, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var adminId = NewId();
        var projectId = NewId();

        userRepo.Users[ownerId] = User.Create(
            "owner",
            "Owner",
            "User",
            "owner@test.com",
            "hash",
            isAdmin: false,
            id: ownerId
        );
        userRepo.Users[adminId] = User.Create(
            "admin",
            "Admin",
            "User",
            "admin@test.com",
            "hash",
            isAdmin: true,
            id: adminId
        );

        var ownSession = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        var projectSession = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(ownSession);
        sessionRepo.Sessions.Add(projectSession);

        var result = await service.ListAsync(
            adminId,
            folderId: null,
            projectId: null,
            before: null,
            beforeId: null,
            limit: 50
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
    }

    // ── LinkCardAsync tests ────────────────────────────────────────────────────

    [Fact]
    public async Task LinkCardAsync_ActiveProjectSession_LinksSuccessfully()
    {
        var (service, sessionRepo, _, cardRepo, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };

        var result = await service.LinkCardAsync(session.Id, cardId, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(cardId, result.Value.OpenCardId);
        Assert.Equal(session.Id, result.Value.Id);
    }

    [Fact]
    public async Task LinkCardAsync_ClosedSession_ReturnsSessionClosedError()
    {
        var (service, sessionRepo, _, cardRepo, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Closed,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };

        var result = await service.LinkCardAsync(session.Id, cardId, ownerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionClosed, result.Error.Code);
    }

    [Fact]
    public async Task LinkCardAsync_PersonalSession_ReturnsCardNotInProjectError()
    {
        var (service, sessionRepo, _, cardRepo, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = null,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = NewId() };

        var result = await service.LinkCardAsync(session.Id, cardId, ownerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.CardNotInProject, result.Error.Code);
    }

    [Fact]
    public async Task LinkCardAsync_CardDifferentProject_ReturnsCardNotInProjectError()
    {
        var (service, sessionRepo, _, cardRepo, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var otherProjectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = otherProjectId };

        var result = await service.LinkCardAsync(session.Id, cardId, ownerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.CardNotInProject, result.Error.Code);
    }

    [Fact]
    public async Task LinkCardAsync_NonMemberDenied()
    {
        var (service, sessionRepo, _, cardRepo, _, memberRepo, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var nonMemberId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };
        memberRepo.DenyAccess = true;

        var result = await service.LinkCardAsync(session.Id, cardId, nonMemberId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task LinkCardAsync_Idempotent_ReturnsSuccessWithoutUpdate()
    {
        var (service, sessionRepo, _, cardRepo, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            OpenCardId = cardId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };

        var result = await service.LinkCardAsync(session.Id, cardId, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(cardId, result.Value.OpenCardId);
        Assert.Equal(0, sessionRepo.UpdateCallCount);
    }

    [Fact]
    public async Task LinkCardAsync_CreatesCardChatLinkImmediately_SoCardChatTabIsNotEmptyBeforeClose()
    {
        var (service, sessionRepo, _, cardRepo, _, _, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };

        var result = await service.LinkCardAsync(session.Id, cardId, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Single(sessionRepo.CapturedLinks);
        var link = sessionRepo.CapturedLinks[0];
        Assert.Equal(cardId, link.CardId);
        Assert.Equal(session.Id, link.ChatSessionId);
        Assert.Equal(ownerId, link.OwnerId);
    }

    [Fact]
    public async Task CloseAsync_UpdatesLinkCreatedByLinkCardAsync_InsteadOfInsertingDuplicate()
    {
        var (service, sessionRepo, messageRepo, cardRepo, _, _, _, _, summaryGenerator, _, _, _) =
            CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };

        await service.LinkCardAsync(session.Id, cardId, ownerId);
        Assert.Single(sessionRepo.CapturedLinks);

        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = session.Id,
                Role = MessageRole.User,
                Content = "Hello",
            }
        );
        summaryGenerator.GenerateSummaryImpl = (_, _) =>
            Task.FromResult(Result<string>.Success("Real summary"));

        var result = await service.CloseAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        // Still exactly one link for this card/session — updated, not duplicated.
        Assert.Single(sessionRepo.CapturedLinks);
        Assert.Equal("Real summary", sessionRepo.CapturedLinks[0].Summary);
    }

    [Fact]
    public async Task LinkCardAsync_MemberCanLink()
    {
        var (service, sessionRepo, _, cardRepo, _, memberRepo, _, _, _, _, _, _) = CreateSut();
        var ownerId = NewId();
        var memberId = NewId();
        var projectId = NewId();
        var cardId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        cardRepo.Cards[cardId] = new Card { Id = cardId, ProjectId = projectId };
        memberRepo.DenyAccess = false;

        var result = await service.LinkCardAsync(session.Id, cardId, memberId);

        Assert.True(result.IsSuccess);
        Assert.Equal(cardId, result.Value.OpenCardId);
    }
}
