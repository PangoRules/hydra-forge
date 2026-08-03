namespace HydraForge.Application.Tests.Chat;

using System.Linq.Expressions;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Chat;
using HydraForge.Application.Projects;
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

        public Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

        public Task<ChatSession?> GetActiveByPanelAsync(
            Guid projectId,
            Guid? openCardId,
            Guid ownerId,
            CancellationToken ct = default
        ) =>
            Task.FromResult(Sessions.FirstOrDefault(s =>
                s.OwnerId == ownerId &&
                s.ProjectId == projectId &&
                s.OpenCardId == openCardId &&
                s.Status == ChatSessionStatus.Active));

        public Task<IReadOnlyList<ChatSession>> ListAsync(
            Guid ownerId,
            Guid? folderId,
            Guid? projectId,
            DateTime? before,
            int limit,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatSession session, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default)
        {
            CapturedLinks.Add(link);
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
            int limit,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<ChatMessage>>(
                Messages.Where(m => m.SessionId == sessionId).ToList());

        public Task AddAsync(ChatMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessionDocRepo : IChatSessionDocumentRepository
    {
        public Task<IReadOnlyList<ChatSessionDocument>> GetBySessionAsync(
            Guid sessionId,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSessionDocument>>([]);

        public Task AddAsync(ChatSessionDocument sessionDocument, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(Guid sessionId, Guid documentId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid sessionId, Guid documentId, CancellationToken ct = default) =>
            Task.FromResult(false);
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
        ) => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(
            _card != null && cardIds.Contains(_card.Id)
                ? new Dictionary<Guid, Card> { [_card.Id] = _card }
                : new Dictionary<Guid, Card>());

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
        public Task<int> CountActiveChildrenAsync(Guid parentCardId, CancellationToken ct = default) =>
            Task.FromResult(0);
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<User?>(null);
        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<Guid, User>>(new Dictionary<Guid, User>());
        public Task<User?> FindByUsernameAsync(string username) =>
            Task.FromResult<User?>(null);
        public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
            IReadOnlyList<string> usernames,
            string? searchTerm = null,
            int maxResults = 10,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<string, User>>(new Dictionary<string, User>());
        public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;
        public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);
        public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(false);
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
        public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ProjectMember?>(null);

        public Task<ProjectMember?> GetByProjectAndUserAsync(
            Guid projectId,
            Guid userId,
            CancellationToken ct = default
        ) => Task.FromResult<ProjectMember?>(
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, Role = MemberRole.Owner }
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
        ) => Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(
            projectIds.ToDictionary(id => id, _ => MemberRole.Owner));

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
        public Task<AgentPersonality?> GetByIdAsync(Guid personalityId, CancellationToken ct = default) =>
            Task.FromResult<AgentPersonality?>(null);

        public Task<IReadOnlyList<AgentPersonality>> ListByUserAsync(
            Guid userId,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<AgentPersonality>>([]);

        public Task<AgentPersonality?> GetDefaultAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<AgentPersonality?>(null);

        public Task AddAsync(AgentPersonality personality, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(AgentPersonality personality, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task ArchiveAsync(Guid personalityId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SetDefaultAsync(Guid personalityId, Guid userId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeDocumentRepo : IDocumentRepository
    {
        private readonly Document? _document;

        public FakeDocumentRepo(Document? document = null) => _document = document;

        public Task<Document?> GetByIdAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult(_document);

        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>([]);

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

        public Task EnqueueAsync(Func<CancellationToken, Task> workItem, CancellationToken ct = default)
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
                Func<CancellationToken, Task> del = async ct => { await func(job); };
                CapturedJobDelegates.Add(del);
            }
            return Task.CompletedTask;
        }

        private IServiceProvider? _serviceProvider;

        public void SetServiceProvider(IServiceProvider? sp) => _serviceProvider = sp;
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();
        private readonly object? _chatSessionService;

        public FakeServiceProvider(
            ChatSessionService? chatSessionService,
            IChatSessionRepository? sessionRepo = null,
            IChatMessageRepository? messageRepo = null,
            IChatSessionDocumentRepository? sessionDocRepo = null,
            ICardRepository? cardRepo = null,
            IUserRepository? userRepo = null,
            IProjectMemberRepository? memberRepo = null,
            IAgentPersonalityRepository? personalityRepo = null,
            IDocumentRepository? documentRepo = null,
            IChatSummaryGenerator? summaryGenerator = null,
            IBackgroundTaskQueue? backgroundTaskQueue = null
        )
        {
            _chatSessionService = chatSessionService;
            if (sessionRepo != null) _services[typeof(IChatSessionRepository)] = sessionRepo;
            if (messageRepo != null) _services[typeof(IChatMessageRepository)] = messageRepo;
            if (sessionDocRepo != null) _services[typeof(IChatSessionDocumentRepository)] = sessionDocRepo;
            if (cardRepo != null) _services[typeof(ICardRepository)] = cardRepo;
            if (userRepo != null) _services[typeof(IUserRepository)] = userRepo;
            if (memberRepo != null) _services[typeof(IProjectMemberRepository)] = memberRepo;
            if (personalityRepo != null) _services[typeof(IAgentPersonalityRepository)] = personalityRepo;
            if (documentRepo != null) _services[typeof(IDocumentRepository)] = documentRepo;
            if (summaryGenerator != null) _services[typeof(IChatSummaryGenerator)] = summaryGenerator;
            if (backgroundTaskQueue != null) _services[typeof(IBackgroundTaskQueue)] = backgroundTaskQueue;
        }

        public object? GetService(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var svc))
                return svc;
            if (serviceType == typeof(ChatSessionService))
                return _chatSessionService;
            return null;
        }

        // Test-only method to inject the real ChatSessionService after construction.
        public void InjectChatSessionServiceForTesting(ChatSessionService svc) =>
            _services[typeof(ChatSessionService)] = svc;

        // Test-only method to inject any service after construction.
        public void InjectService<T>(T svc) where T : class =>
            _services[typeof(T)] = svc;
    }

    private sealed class FakeSummaryGenerator : IChatSummaryGenerator
    {
        public Func<Guid, CancellationToken, Task<Result<string>>>? GenerateSummaryImpl { get; set; }

        public Task<Result<string>> GenerateSummaryAsync(Guid sessionId, CancellationToken ct = default)
        {
            if (GenerateSummaryImpl != null)
                return GenerateSummaryImpl(sessionId, ct);
            return Task.FromResult(Result<string>.Success("Test summary"));
        }
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
        FakeBackgroundTaskQueue backgroundTaskQueue
    ) CreateSut()
    {
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo();
        var sessionDocRepo = new FakeSessionDocRepo();
        var cardRepo = new FakeCardRepo();
        var userRepo = new FakeUserRepo();
        var memberRepo = new FakeMemberRepo();
        var personalityRepo = new FakePersonalityRepo();
        var documentRepo = new FakeDocumentRepo();
        var summaryGenerator = new FakeSummaryGenerator();
        var backgroundTaskQueue = new FakeBackgroundTaskQueue();
        var logger = Substitute.For<ILogger<ChatSessionService>>();

        // Build FakeServiceProvider with all fakes so CloseSessionJob can resolve ChatSessionService.
        var serviceProvider = new FakeServiceProvider(
            chatSessionService: null, // ChatSessionService resolved via GetService override below
            sessionRepo: sessionRepo,
            messageRepo: messageRepo,
            sessionDocRepo: sessionDocRepo,
            cardRepo: cardRepo,
            userRepo: userRepo,
            memberRepo: memberRepo,
            personalityRepo: personalityRepo,
            documentRepo: documentRepo,
            summaryGenerator: summaryGenerator,
            backgroundTaskQueue: backgroundTaskQueue
        );

        // Hook the queue into the provider so EnqueueJobAsync can resolve job types.
        backgroundTaskQueue.SetServiceProvider(serviceProvider);

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
            serviceProvider,
            logger
        );

        // Now wire the service into FakeServiceProvider so CloseSessionJob can use it.
        serviceProvider.InjectChatSessionServiceForTesting(service);

        // Register Application-layer CloseSessionJob so EnqueueJobAsync can resolve it.
        var closeSessionJob = new CloseSessionJob(serviceProvider);
        backgroundTaskQueue.SetCloseSessionJobInstance(closeSessionJob);

        return (service, sessionRepo, messageRepo, cardRepo, userRepo, memberRepo, personalityRepo, documentRepo, summaryGenerator, backgroundTaskQueue);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_F6_ImplicitClose_EnqueuesBackgroundJob()
    {
        var (service, sessionRepo, _, cardRepo, _, _, _, _, _, backgroundTaskQueue) = CreateSut();
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
                ForkedFromSessionId: null
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
        var (service, sessionRepo, _, cardRepo, _, _, _, _, summaryGenerator, backgroundTaskQueue) = CreateSut();
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
                ForkedFromSessionId: null
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
        var (service, sessionRepo, messageRepo, _, _, _, _, _, summaryGenerator, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Title = "Test Session",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);
        messageRepo.Messages.Add(new ChatMessage
        {
            Id = NewId(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "Hello"
        });

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
        var (service, sessionRepo, messageRepo, _, _, _, _, _, _, _) = CreateSut();
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
        var (service, sessionRepo, _, _, _, _, _, _, _, _) = CreateSut();
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
                SearchAllMyDocs: false
            ),
            ownerId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionClosed, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_Fork_CreatesIndependentSessionOwnedByCaller()
    {
        var (service, sessionRepo, messageRepo, _, _, _, _, _, summaryGenerator, _) = CreateSut();
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
        messageRepo.Messages.Add(new ChatMessage
        {
            Id = NewId(),
            SessionId = source.Id,
            Role = MessageRole.User,
            Content = "Hello"
        });

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
                ForkedFromSessionId: source.Id
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
        var summaryMessages = messageRepo.Messages
            .Where(m => m.SessionId == forked.Id && m.Role == MessageRole.Assistant)
            .ToList();
        Assert.Single(summaryMessages);
        Assert.Contains("Forked summary", summaryMessages[0].Content);
    }

    [Fact]
    public async Task GetPermissionAsync_Granted_WhenActiveProjectSession()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _) = CreateSut();
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
        var (service, sessionRepo, _, _, _, _, _, _, _, _) = CreateSut();
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
        var (service, sessionRepo, messageRepo, _, _, _, _, _, summaryGenerator, _) = CreateSut();
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
        messageRepo.Messages.Add(new ChatMessage
        {
            Id = NewId(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "Hello"
        });

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
    public async Task ArchiveAsync_NotOwner_ReturnsSessionNotOwner()
    {
        var (service, sessionRepo, _, _, _, _, _, _, _, _) = CreateSut();
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
        var (service, sessionRepo, _, _, _, _, _, _, _, _) = CreateSut();
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
}
