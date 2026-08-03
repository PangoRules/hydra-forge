using System.Security.Claims;
using HydraForge.Application.Admin;
using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Constants;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HydraForge.Server.Tests.Chat;

public class ChatHubTests
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
    private readonly ILogger<ChatHub> _logger;
    private readonly IOptions<LlmOptions> _llmOptions;
    private readonly IChatHub _mockCaller;
    private readonly ChatHub _hub;

    public ChatHubTests()
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
        _logger = Substitute.For<ILogger<ChatHub>>();
        _llmOptions = Substitute.For<IOptions<LlmOptions>>();
        _llmOptions.Value.Returns(new LlmOptions());
        _mockCaller = Substitute.For<IChatHub>();

        var mockContext = Substitute.For<HubCallerContext>();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim(ClaimTypes.Role, "User"),
            ],
            "Test"
        );
        var user = new ClaimsPrincipal(identity);
        mockContext.User.Returns(user);
        mockContext.ConnectionId.Returns("test-connection-id");

        var mockClients = Substitute.For<IHubCallerClients<IChatHub>>();
        mockClients.Caller.Returns(_mockCaller);
        mockClients.Group(Arg.Any<string>()).Returns(_mockCaller);

        var mockGroups = Substitute.For<IGroupManager>();

        _hub = new ChatHub(
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
            _logger,
            _llmOptions
        )
        {
            Clients = mockClients,
            Context = mockContext,
            Groups = mockGroups,
        };
    }

    [Fact]
    public async Task JoinSession_AsOwner_Succeeds()
    {
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);

        await _hub.JoinSession(SessionId);

        _mockCaller.DidNotReceiveWithAnyArgs().StreamError(default, default!, default!);
    }

    [Fact]
    public async Task JoinSession_AsNonMember_ThrowsHubException()
    {
        var projectId = Guid.NewGuid();
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = Guid.NewGuid(),
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _memberRepo
            .GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns((ProjectMember?)null);

        await Assert.ThrowsAsync<HubException>(() => _hub.JoinSession(SessionId));
    }

    [Fact]
    public async Task JoinSession_AsSharedProjectMember_Succeeds()
    {
        var projectId = Guid.NewGuid();
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = Guid.NewGuid(),
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _memberRepo
            .GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { ProjectId = projectId, UserId = UserId });

        await _hub.JoinSession(SessionId);

        _mockCaller.DidNotReceiveWithAnyArgs().StreamError(default, default!, default!);
    }

    [Fact]
    public async Task JoinSession_AsProjectMemberOnNonSharedSession_ThrowsHubException()
    {
        var projectId = Guid.NewGuid();
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = Guid.NewGuid(),
            ProjectId = projectId,
            IsShared = false,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _memberRepo
            .GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { ProjectId = projectId, UserId = UserId });

        await Assert.ThrowsAsync<HubException>(() => _hub.JoinSession(SessionId));
    }

    [Fact]
    public async Task SendMessage_MessageNotFound_ReturnsStreamError()
    {
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo
            .GetByIdAsync(MessageId, Arg.Any<CancellationToken>())
            .Returns((HydraForge.Domain.Entities.Chat.ChatMessage?)null);

        await _hub.SendMessage(SessionId, MessageId, null);

        await _mockCaller
            .Received(1)
            .StreamError(MessageId, "CHAT_MESSAGE_NOT_FOUND", Arg.Any<string>());
    }

    [Fact]
    public async Task SendMessage_ClosedSession_ReturnsStreamError()
    {
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Closed,
        };
        var userMessage = new HydraForge.Domain.Entities.Chat.ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);

        await _hub.SendMessage(SessionId, MessageId, null);

        await _mockCaller
            .Received(1)
            .StreamError(MessageId, "CHAT_SESSION_CLOSED", Arg.Any<string>());
    }

    [Fact]
    public async Task SendMessage_ArchivedSession_ReturnsStreamError()
    {
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
            ArchivedAt = DateTime.UtcNow,
        };
        var userMessage = new HydraForge.Domain.Entities.Chat.ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);

        await _hub.SendMessage(SessionId, MessageId, null);

        await _mockCaller
            .Received(1)
            .StreamError(MessageId, "CHAT_SESSION_ARCHIVED", Arg.Any<string>());
    }

    [Fact]
    public async Task SendMessage_OneActiveStream_RejectsSecond()
    {
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new HydraForge.Domain.Entities.Chat.ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);
        var secondUserMessage = new HydraForge.Domain.Entities.Chat.ChatMessage
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
            .Returns(
                (IReadOnlyList<HydraForge.Domain.Entities.Chat.ChatMessage>)
                    new List<HydraForge.Domain.Entities.Chat.ChatMessage>()
            );

        // Start first stream and wait for it to block
        var sendTask = _hub.SendMessage(SessionId, MessageId, null);
        await streamYielded.WaitAsync(TimeSpan.FromSeconds(5));

        // Try second send while first is blocked
        await _hub.SendMessage(SessionId, secondUserMessage.Id, null);

        await _mockCaller
            .Received(1)
            .StreamError(secondUserMessage.Id, "CHAT_STREAM_IN_PROGRESS", Arg.Any<string>());

        // Release the blocked stream and wait for completion
        streamBlock.Release();
        await sendTask;
    }

    [Fact]
    public async Task CancelStream_CancelsActiveStream()
    {
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new HydraForge.Domain.Entities.Chat.ChatMessage
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
            .Returns(
                (IReadOnlyList<HydraForge.Domain.Entities.Chat.ChatMessage>)
                    new List<HydraForge.Domain.Entities.Chat.ChatMessage>()
            );

        // Start stream and wait for it to block
        var sendTask = _hub.SendMessage(SessionId, MessageId, null);
        await streamYielded.WaitAsync(TimeSpan.FromSeconds(5));

        // Cancel
        await _hub.CancelStream(SessionId);

        // Wait for SendMessage to complete (should emit StreamDone via OCE handler)
        await sendTask;

        // Assert StreamDone was emitted (with null values from OCE path)
        var doneCalls = _mockCaller
            .ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "StreamDone")
            .ToList();
        Assert.Single(doneCalls);
    }

    [Fact]
    public async Task SendMessage_NonMemberOfSharedSession_ReturnsStreamError()
    {
        var projectId = Guid.NewGuid();
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = Guid.NewGuid(),
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new HydraForge.Domain.Entities.Chat.ChatMessage
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

        await _hub.SendMessage(SessionId, MessageId, null);

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
    public async Task CancelStream_NonMemberOfSharedSession_DoesNotCancel()
    {
        var projectId = Guid.NewGuid();
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = Guid.NewGuid(),
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _memberRepo
            .GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns((ProjectMember?)null);

        // No active stream registered; CancelStream should just no-op without throwing.
        await _hub.CancelStream(SessionId);
    }

    [Fact]
    public async Task SendMessage_And_CancelStream_AsSharedProjectMember_Succeed()
    {
        var projectId = Guid.NewGuid();
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = Guid.NewGuid(),
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new HydraForge.Domain.Entities.Chat.ChatMessage
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
            .Returns(
                (IReadOnlyList<HydraForge.Domain.Entities.Chat.ChatMessage>)
                    new List<HydraForge.Domain.Entities.Chat.ChatMessage>()
            );

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

        var sendTask = _hub.SendMessage(SessionId, MessageId, null);
        await streamYielded.WaitAsync(TimeSpan.FromSeconds(5));

        await _mockCaller
            .DidNotReceive()
            .StreamError(Arg.Any<Guid>(), "CHAT_SESSION_NOT_OWNER", Arg.Any<string>());

        // A shared member who isn't the owner must still be able to cancel the stream.
        await _hub.CancelStream(SessionId);
        await sendTask;

        var doneCalls = _mockCaller
            .ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "StreamDone")
            .ToList();
        Assert.Single(doneCalls);
    }

    [Fact]
    public async Task SendMessage_TokenBudgetExceeded_ReturnsStreamError()
    {
        var session = new HydraForge.Domain.Entities.Chat.ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        var userMessage = new HydraForge.Domain.Entities.Chat.ChatMessage
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
            .Returns(
                (IReadOnlyList<HydraForge.Domain.Entities.Chat.ChatMessage>)
                    new List<HydraForge.Domain.Entities.Chat.ChatMessage>()
            );
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

        await _hub.SendMessage(SessionId, MessageId, null);

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

    /// <summary>
    /// Yields one chunk, signals it yielded, then blocks until cancellation or semaphore release.
    /// </summary>
    private static async IAsyncEnumerable<ChatChunk> MakeBlockingEnumerable(
        SemaphoreSlim block,
        SemaphoreSlim yielded,
        CancellationToken cancellationToken
    )
    {
        yield return new ChatChunk("Hello", null, null);
        yielded.Release();
        await block.WaitAsync(cancellationToken);
    }
}
