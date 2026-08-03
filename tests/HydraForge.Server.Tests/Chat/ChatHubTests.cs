using System.Security.Claims;
using HydraForge.Application.Admin;
using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Constants;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Common;
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
    private readonly ILogger<ChatHub> _logger;
    private readonly IOptions<LlmOptions> _llmOptions;
    private readonly IChatHub _mockCaller;
    private readonly ChatHub _hub;

    public ChatHubTests()
    {
        _memberRepo = Substitute.For<IProjectMemberRepository>();
        _sessionRepo = Substitute.For<IChatSessionRepository>();
        _messageRepo = Substitute.For<IChatMessageRepository>();
        _presetRepo = Substitute.For<IPromptPresetRepository>();
        _personalityRepo = Substitute.For<IAgentPersonalityRepository>();
        _ragRetriever = Substitute.For<IChatRagRetriever>();
        _modelRouter = Substitute.For<IModelRouter>();
        _llmClientFactory = Substitute.For<ILlmClientFactory>();
        _usageRecorder = Substitute.For<IUsageRecorder>();

        var budgetRepo = Substitute.For<IUserTokenBudgetRepository>();
        budgetRepo.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UserTokenBudget?)null);
        _llmCallGuard = new LlmCallGuard(budgetRepo, _usageRecorder);
        _logger = Substitute.For<ILogger<ChatHub>>();
        _llmOptions = Substitute.For<IOptions<LlmOptions>>();
        _llmOptions.Value.Returns(new LlmOptions());
        _mockCaller = Substitute.For<IChatHub>();

        var mockContext = Substitute.For<HubCallerContext>();
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Role, "User"),
        ], "Test");
        var user = new ClaimsPrincipal(identity);
        mockContext.User.Returns(user);
        mockContext.ConnectionId.Returns("test-connection-id");

        var mockClients = Substitute.For<IHubCallerClients<IChatHub>>();
        mockClients.Caller.Returns(_mockCaller);

        var mockGroups = Substitute.For<IGroupManager>();

        _hub = new ChatHub(
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
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _memberRepo.GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns((ProjectMember?)null);

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
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns((HydraForge.Domain.Entities.Chat.ChatMessage?)null);

        await _hub.SendMessage(SessionId, MessageId, null);

        await _mockCaller.Received(1).StreamError(MessageId, "CHAT_MESSAGE_NOT_FOUND", Arg.Any<string>());
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

        await _mockCaller.Received(1).StreamError(MessageId, "CHAT_SESSION_CLOSED", Arg.Any<string>());
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

        await _mockCaller.Received(1).StreamError(MessageId, "CHAT_SESSION_ARCHIVED", Arg.Any<string>());
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
        _messageRepo.GetByIdAsync(secondUserMessage.Id, Arg.Any<CancellationToken>()).Returns(secondUserMessage);
        _ragRetriever.RetrieveAsync(SessionId, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CacheBlock>)new List<CacheBlock>());

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "openai",
            AdapterType = AdapterType.OpenAiCompatible,
        };
        var routeDecision = new RouteDecision(
            new ProviderModelConfigDto(Guid.NewGuid(), Guid.NewGuid(), "gpt-4", "GPT-4", "standard", null, null, true),
            new ProviderDto(Guid.NewGuid(), "openai", "https://api.openai.com", "openai-compatible", "cloud", "standard", null, true, default, default),
            [],
            provider
        );
        _modelRouter.ResolveAsync(Arg.Any<AiFeature>(), UserId, Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDecision>.Success(routeDecision));

        var mockClient = Substitute.For<ILlmClient>();
        mockClient.AdapterType.Returns(AdapterType.OpenAiCompatible);
        var streamBlock = new SemaphoreSlim(0, 1);
        var streamYielded = new SemaphoreSlim(0, 1);
        mockClient.StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeBlockingEnumerable(streamBlock, streamYielded));
        _llmClientFactory.For(Arg.Any<LlmProvider>()).Returns(mockClient);
        _messageRepo.GetBySessionAsync(SessionId, Arg.Any<DateTime?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<HydraForge.Domain.Entities.Chat.ChatMessage>)new List<HydraForge.Domain.Entities.Chat.ChatMessage>());

        // Start first stream — fire and forget, it will block on the stream
        _ = _hub.SendMessage(SessionId, MessageId, null);
        await streamYielded.WaitAsync(TimeSpan.FromSeconds(5)); // wait until first chunk is yielded

        // Try second send
        await _hub.SendMessage(SessionId, secondUserMessage.Id, null);

        await _mockCaller.Received(1).StreamError(secondUserMessage.Id, "CHAT_STREAM_IN_PROGRESS", Arg.Any<string>());

        // Release the blocked stream
        streamBlock.Release();
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
        _ragRetriever.RetrieveAsync(SessionId, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CacheBlock>)new List<CacheBlock>());

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "openai",
            AdapterType = AdapterType.OpenAiCompatible,
        };
        var routeDecision = new RouteDecision(
            new ProviderModelConfigDto(Guid.NewGuid(), Guid.NewGuid(), "gpt-4", "GPT-4", "standard", null, null, true),
            new ProviderDto(Guid.NewGuid(), "openai", "https://api.openai.com", "openai-compatible", "cloud", "standard", null, true, default, default),
            [],
            provider
        );
        _modelRouter.ResolveAsync(Arg.Any<AiFeature>(), UserId, Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDecision>.Success(routeDecision));

        var mockClient = Substitute.For<ILlmClient>();
        mockClient.AdapterType.Returns(AdapterType.OpenAiCompatible);
        var streamBlock = new SemaphoreSlim(0, 1);
        var streamYielded = new SemaphoreSlim(0, 1);
        mockClient.StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeBlockingEnumerable(streamBlock, streamYielded));
        _llmClientFactory.For(Arg.Any<LlmProvider>()).Returns(mockClient);
        _messageRepo.GetBySessionAsync(SessionId, Arg.Any<DateTime?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<HydraForge.Domain.Entities.Chat.ChatMessage>)new List<HydraForge.Domain.Entities.Chat.ChatMessage>());

        // Start stream
        _ = _hub.SendMessage(SessionId, MessageId, null);
        await Task.Delay(1000); // wait for the stream to start emitting

        // Verify stream started
        var allCalls1 = _mockCaller.ReceivedCalls().Select(c => $"{c.GetMethodInfo().Name}({string.Join(", ", c.GetArguments())})").ToList();
        Assert.Contains(allCalls1, c => c.Contains("StreamStart"));
        _mockCaller.ClearReceivedCalls();

        // Cancel
        await _hub.CancelStream(SessionId);

        // Add small delay to let cancellation propagate
        await Task.Delay(500);

        var allCalls = _mockCaller.ReceivedCalls().Select(c => c.GetMethodInfo().Name).ToList();
        Assert.Contains("StreamDone", allCalls);

        // Release the blocked stream so it can clean up
        streamBlock.Release();
    }

    /// <summary>
    /// Yields one chunk, signals it yielded, then blocks on a semaphore.
    /// </summary>
    private static async IAsyncEnumerable<ChatChunk> MakeBlockingEnumerable(
        SemaphoreSlim block,
        SemaphoreSlim yielded)
    {
        yield return new ChatChunk("Hello", null, null);
        yielded.Release();
        await block.WaitAsync();
    }
}