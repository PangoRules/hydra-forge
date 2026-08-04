using System.Linq.Expressions;
using System.Security.Claims;
using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace HydraForge.Server.Tests.Chat;

using ChatSession = HydraForge.Domain.Entities.Chat.ChatSession;

/// <summary>
/// Reply-generation behavior (RAG, budget, model routing, streaming, cancellation-under-load,
/// etc.) lives in <see cref="ChatReplyGenerator"/> now and is tested in
/// HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs — SendMessage here is just a
/// thin enqueue, so these tests cover only what actually still runs in the hub: group
/// membership (Join/Leave), the access-checked enqueue, and CancelStream's registry wiring.
/// </summary>
public class ChatHubTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid MessageId = Guid.NewGuid();

    private readonly IUserRepository _userRepo;
    private readonly IProjectMemberRepository _memberRepo;
    private readonly IChatSessionRepository _sessionRepo;
    private readonly IChatStreamRegistry _streamRegistry;
    private readonly CapturingBackgroundTaskQueue _backgroundTaskQueue;
    private readonly IChatHub _mockCaller;
    private readonly ChatHub _hub;

    public ChatHubTests()
    {
        _userRepo = Substitute.For<IUserRepository>();
        _memberRepo = Substitute.For<IProjectMemberRepository>();
        _sessionRepo = Substitute.For<IChatSessionRepository>();
        _streamRegistry = new ChatStreamRegistry();
        _backgroundTaskQueue = new CapturingBackgroundTaskQueue();
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
            _streamRegistry,
            _backgroundTaskQueue
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
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);

        await _hub.JoinSession(SessionId);

        _ = _mockCaller.DidNotReceiveWithAnyArgs().StreamError(default, default!, default!);
    }

    [Fact]
    public async Task JoinSession_AsNonMember_ThrowsHubException()
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
        var session = new ChatSession
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

        _ = _mockCaller.DidNotReceiveWithAnyArgs().StreamError(default, default!, default!);
    }

    [Fact]
    public async Task JoinSession_AsProjectMemberOnNonSharedSession_ThrowsHubException()
    {
        var projectId = Guid.NewGuid();
        var session = new ChatSession
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
    public async Task SendMessage_EnqueuesChatReplyGeneratorWithCallerArgs()
    {
        var presetId = Guid.NewGuid();
        var modelConfigId = Guid.NewGuid();

        await _hub.SendMessage(SessionId, MessageId, presetId, modelConfigId);

        Assert.Equal(typeof(ChatReplyGenerator), _backgroundTaskQueue.JobType);
        var expr = Assert.IsAssignableFrom<LambdaExpression>(
            _backgroundTaskQueue.CapturedExpression
        );
        var call = Assert.IsAssignableFrom<MethodCallExpression>(expr.Body);
        Assert.Equal(nameof(ChatReplyGenerator.GenerateAsync), call.Method.Name);

        object? Eval(Expression e) => Expression.Lambda(e).Compile().DynamicInvoke();

        Assert.Equal(SessionId, Eval(call.Arguments[0]));
        Assert.Equal(MessageId, Eval(call.Arguments[1]));
        Assert.Equal(UserId, Eval(call.Arguments[2]));
        Assert.Equal(presetId, Eval(call.Arguments[3]));
        Assert.Equal(modelConfigId, Eval(call.Arguments[4]));
    }

    [Fact]
    public async Task CancelStream_AsOwner_CancelsRegisteredStream()
    {
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);

        using var cts = new CancellationTokenSource();
        Assert.True(_streamRegistry.TryRegister(SessionId, cts));

        await _hub.CancelStream(SessionId);

        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task CancelStream_AsSharedProjectMember_CancelsRegisteredStream()
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
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _memberRepo
            .GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { ProjectId = projectId, UserId = UserId });

        using var cts = new CancellationTokenSource();
        Assert.True(_streamRegistry.TryRegister(SessionId, cts));

        await _hub.CancelStream(SessionId);

        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task CancelStream_NonMemberOfSharedSession_DoesNotCancel()
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
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _memberRepo
            .GetByProjectAndUserAsync(projectId, UserId, Arg.Any<CancellationToken>())
            .Returns((ProjectMember?)null);

        using var cts = new CancellationTokenSource();
        _streamRegistry.TryRegister(SessionId, cts);

        await _hub.CancelStream(SessionId);

        Assert.False(cts.IsCancellationRequested);
    }

    private sealed class CapturingBackgroundTaskQueue : IBackgroundTaskQueue
    {
        public object? CapturedExpression { get; private set; }
        public Type? JobType { get; private set; }

        public Task EnqueueAsync(
            Func<CancellationToken, Task> workItem,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task EnqueueJobAsync<TJob>(Expression<Func<TJob, Task>> methodCall)
        {
            CapturedExpression = methodCall;
            JobType = typeof(TJob);
            return Task.CompletedTask;
        }
    }
}
