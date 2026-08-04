using HydraForge.Application.Admin;
using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Constants;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ChatMessage = HydraForge.Domain.Entities.Chat.ChatMessage;
using ChatSession = HydraForge.Domain.Entities.Chat.ChatSession;

namespace HydraForge.Application.Tests.Chat;

public class ChatReplyGeneratorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid MessageId = Guid.NewGuid();

    private readonly IUserRepository _userRepo;
    private readonly IProjectMemberRepository _memberRepo;
    private readonly IChatSessionRepository _sessionRepo;
    private readonly IChatMessageRepository _messageRepo;
    private readonly IPromptPresetRepository _presetRepo;
    private readonly IAgentPersonalityRepository _personalityRepo;
    private readonly IChatRagRetriever _ragRetriever;
    private readonly IModelRouter _modelRouter;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly IUsageRecorder _usageRecorder;
    private readonly LlmCallGuard _llmCallGuard;
    private readonly IUserTokenBudgetRepository _budgetRepo;
    private readonly IContextCompressor _contextCompressor;
    private readonly IChatTitleGenerator _titleGenerator;
    private readonly IChatStreamRegistry _streamRegistry;
    private readonly ILogger<ChatReplyGenerator> _logger;
    private readonly IOptions<LlmOptions> _llmOptions;
    private readonly IChatHub _mockCaller;
    private readonly IChatBroadcaster _broadcaster;
    private readonly ChatReplyGenerator _generator;

    public ChatReplyGeneratorTests()
    {
        _userRepo = Substitute.For<IUserRepository>();
        _userRepo.IsAdminAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _memberRepo = Substitute.For<IProjectMemberRepository>();
        _sessionRepo = Substitute.For<IChatSessionRepository>();
        _messageRepo = Substitute.For<IChatMessageRepository>();
        _presetRepo = Substitute.For<IPromptPresetRepository>();
        _personalityRepo = Substitute.For<IAgentPersonalityRepository>();
        _ragRetriever = Substitute.For<IChatRagRetriever>();
        _modelRouter = Substitute.For<IModelRouter>();
        _llmClientFactory = Substitute.For<ILlmClientFactory>();
        _usageRecorder = Substitute.For<IUsageRecorder>();
        _contextCompressor = Substitute.For<IContextCompressor>();
        _contextCompressor
            .CompressAsync(
                Arg.Any<IReadOnlyList<CacheBlock>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<CompressedContext>.Success(new CompressedContext([], 0, false)));

        _budgetRepo = Substitute.For<IUserTokenBudgetRepository>();
        _budgetRepo
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UserTokenBudget?)null);
        _llmCallGuard = new LlmCallGuard(_budgetRepo, _usageRecorder);
        _titleGenerator = Substitute.For<IChatTitleGenerator>();
        _titleGenerator
            .GenerateTitleAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<string>.Failure(new Error("TEST_NO_TITLE", "not configured in test")));
        _streamRegistry = new FakeChatStreamRegistry();
        _logger = Substitute.For<ILogger<ChatReplyGenerator>>();
        _llmOptions = Substitute.For<IOptions<LlmOptions>>();
        _llmOptions.Value.Returns(new LlmOptions());
        _mockCaller = Substitute.For<IChatHub>();
        _broadcaster = Substitute.For<IChatBroadcaster>();
        _broadcaster.Group(Arg.Any<Guid>()).Returns(_mockCaller);

        _generator = new ChatReplyGenerator(
            _userRepo,
            _memberRepo,
            _sessionRepo,
            _messageRepo,
            _presetRepo,
            _personalityRepo,
            _ragRetriever,
            _modelRouter,
            _llmClientFactory,
            _usageRecorder,
            _llmCallGuard,
            _contextCompressor,
            _titleGenerator,
            _streamRegistry,
            _broadcaster,
            _logger,
            _llmOptions
        );
    }

    [Fact]
    public async Task GenerateAsync_MessageNotFound_ReturnsStreamError()
    {
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo
            .GetByIdAsync(MessageId, Arg.Any<CancellationToken>())
            .Returns((ChatMessage?)null);

        await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);

        await _mockCaller
            .Received(1)
            .StreamError(MessageId, "CHAT_MESSAGE_NOT_FOUND", Arg.Any<string>());
    }

    [Fact]
    public async Task GenerateAsync_ClosedSession_ReturnsStreamError()
    {
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Closed,
        };
        var userMessage = new ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);

        await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);

        await _mockCaller
            .Received(1)
            .StreamError(MessageId, "CHAT_SESSION_CLOSED", Arg.Any<string>());
    }

    [Fact]
    public async Task GenerateAsync_ArchivedSession_ReturnsStreamError()
    {
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
            ArchivedAt = DateTime.UtcNow,
        };
        var userMessage = new ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);

        await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);

        await _mockCaller
            .Received(1)
            .StreamError(MessageId, "CHAT_SESSION_ARCHIVED", Arg.Any<string>());
    }

    [Fact]
    public async Task GenerateAsync_NonMemberOfSharedSession_ReturnsStreamError()
    {
        var projectId = Guid.NewGuid();
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = Guid.NewGuid(),
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);
        _memberRepo
            .GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns((ProjectMember?)null);

        await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);

        await _mockCaller
            .Received(1)
            .StreamError(MessageId, "CHAT_SESSION_NOT_OWNER", Arg.Any<string>());
        await _messageRepo
            .DidNotReceive()
            .GetBySessionAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTime?>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GenerateAsync_TokenBudgetExceeded_ReturnsStreamError()
    {
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);
        _ragRetriever
            .RetrieveAsync(
                SessionId,
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((IReadOnlyList<CacheBlock>)new List<CacheBlock>());
        _messageRepo
            .GetBySessionAsync(
                SessionId,
                Arg.Any<DateTime?>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((IReadOnlyList<ChatMessage>)new List<ChatMessage>());
        _budgetRepo
            .GetByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(
                new UserTokenBudget
                {
                    UserId = UserId,
                    MonthlyTokenBudget = 1,
                    MonthlyTokenUsed = 1,
                    PeriodEnd = DateTime.UtcNow.AddDays(30),
                }
            );

        await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);

        await _mockCaller
            .Received(1)
            .StreamError(
                Arg.Any<Guid>(),
                DomainErrorCodes.Llm.TokenBudgetExceeded,
                Arg.Any<string>()
            );
        await _modelRouter
            .DidNotReceive()
            .ResolveAsync(
                Arg.Any<AiFeature>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GenerateAsync_OneActiveStream_RejectsSecond()
    {
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);
        var secondUserMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _messageRepo
            .GetByIdAsync(secondUserMessage.Id, Arg.Any<CancellationToken>())
            .Returns(secondUserMessage);
        _ragRetriever
            .RetrieveAsync(
                SessionId,
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((IReadOnlyList<CacheBlock>)new List<CacheBlock>());

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "openai",
            AdapterType = AdapterType.OpenAiCompatible,
        };
        var routeDecision = new RouteDecision(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "gpt-4",
                "GPT-4",
                "standard",
                null,
                null,
                true
            ),
            new ProviderDto(
                Guid.NewGuid(),
                "openai",
                "https://api.openai.com",
                "openai-compatible",
                "cloud",
                "standard",
                null,
                true,
                default,
                default
            ),
            [],
            provider
        );
        _modelRouter
            .ResolveAsync(
                Arg.Any<AiFeature>(),
                UserId,
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<RouteDecision>.Success(routeDecision));

        var mockClient = Substitute.For<ILlmClient>();
        mockClient.AdapterType.Returns(AdapterType.OpenAiCompatible);
        var streamBlock = new SemaphoreSlim(0, 1);
        var streamYielded = new SemaphoreSlim(0, 1);
        var capturedCt = CancellationToken.None;
        mockClient
            .StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCt = callInfo.ArgAt<CancellationToken>(1);
                return MakeBlockingEnumerable(streamBlock, streamYielded, capturedCt);
            });
        _llmClientFactory.For(Arg.Any<LlmProvider>()).Returns(mockClient);
        _messageRepo
            .GetBySessionAsync(
                SessionId,
                Arg.Any<DateTime?>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((IReadOnlyList<ChatMessage>)new List<ChatMessage>());

        // Start first stream and wait for it to block
        var sendTask = _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);
        await streamYielded.WaitAsync(TimeSpan.FromSeconds(5));

        // Try second send while first is blocked
        await _generator.GenerateAsync(SessionId, secondUserMessage.Id, UserId, null, null);

        await _mockCaller
            .Received(1)
            .StreamError(secondUserMessage.Id, "CHAT_STREAM_IN_PROGRESS", Arg.Any<string>());

        // Release the blocked stream and wait for completion
        streamBlock.Release();
        await sendTask;
    }

    [Fact]
    public async Task GenerateAsync_AsSharedProjectMember_Succeeds()
    {
        var projectId = Guid.NewGuid();
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = Guid.NewGuid(),
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);
        _memberRepo
            .GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { ProjectId = projectId, UserId = UserId });
        _ragRetriever
            .RetrieveAsync(
                SessionId,
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((IReadOnlyList<CacheBlock>)new List<CacheBlock>());
        _messageRepo
            .GetBySessionAsync(
                SessionId,
                Arg.Any<DateTime?>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((IReadOnlyList<ChatMessage>)new List<ChatMessage>());

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "openai",
            AdapterType = AdapterType.OpenAiCompatible,
        };
        var routeDecision = new RouteDecision(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "gpt-4",
                "GPT-4",
                "standard",
                null,
                null,
                true
            ),
            new ProviderDto(
                Guid.NewGuid(),
                "openai",
                "https://api.openai.com",
                "openai-compatible",
                "cloud",
                "standard",
                null,
                true,
                default,
                default
            ),
            [],
            provider
        );
        _modelRouter
            .ResolveAsync(
                Arg.Any<AiFeature>(),
                UserId,
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<RouteDecision>.Success(routeDecision));

        var mockClient = Substitute.For<ILlmClient>();
        mockClient.AdapterType.Returns(AdapterType.OpenAiCompatible);
        mockClient
            .StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeImmediateEnumerable());
        _llmClientFactory.For(Arg.Any<LlmProvider>()).Returns(mockClient);

        await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);

        await _mockCaller
            .DidNotReceive()
            .StreamError(Arg.Any<Guid>(), "CHAT_SESSION_NOT_OWNER", Arg.Any<string>());
        var doneCalls = _mockCaller
            .ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "StreamDone")
            .ToList();
        Assert.Single(doneCalls);
    }

    /// <summary>
    /// Yields one chunk, signals it yielded, then blocks until cancellation or semaphore release.
    /// </summary>
    private static async IAsyncEnumerable<ChatChunk> MakeBlockingEnumerable(
        SemaphoreSlim block,
        SemaphoreSlim yielded,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        yield return new ChatChunk("Hello", null, null);
        yielded.Release();
        await block.WaitAsync(cancellationToken);
    }

    private static async IAsyncEnumerable<ChatChunk> MakeImmediateEnumerable()
    {
        yield return new ChatChunk("Hello", null, null);
        await Task.CompletedTask;
    }

    // Mirrors HydraForge.Infrastructure.Realtime.ChatStreamRegistry's semantics without
    // pulling an Infrastructure project reference into Application.Tests.
    private sealed class FakeChatStreamRegistry : IChatStreamRegistry
    {
        private readonly Dictionary<Guid, CancellationTokenSource> _active = [];

        public bool TryRegister(Guid sessionId, CancellationTokenSource cts) =>
            _active.TryAdd(sessionId, cts);

        public bool TryCancel(Guid sessionId)
        {
            if (!_active.TryGetValue(sessionId, out var cts))
                return false;
            cts.Cancel();
            return true;
        }

        public void Remove(Guid sessionId) => _active.Remove(sessionId);
    }
}
