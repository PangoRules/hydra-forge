using HydraForge.Application.Chat;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ChatSession = HydraForge.Domain.Entities.Chat.ChatSession;

namespace HydraForge.Application.Tests.Chat;

public class ChatTitleGenerationJobTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IChatSessionRepository _sessionRepo;
    private readonly IChatTitleGenerator _titleGenerator;
    private readonly IChatBroadcaster _broadcaster;
    private readonly IChatHub _mockCaller;
    private readonly ILogger<ChatTitleGenerationJob> _logger;
    private readonly ChatTitleGenerationJob _job;

    public ChatTitleGenerationJobTests()
    {
        _sessionRepo = Substitute.For<IChatSessionRepository>();
        _titleGenerator = Substitute.For<IChatTitleGenerator>();
        _broadcaster = Substitute.For<IChatBroadcaster>();
        _mockCaller = Substitute.For<IChatHub>();
        _broadcaster.Group(Arg.Any<Guid>()).Returns(_mockCaller);
        _logger = Substitute.For<ILogger<ChatTitleGenerationJob>>();
        _job = new ChatTitleGenerationJob(_sessionRepo, _titleGenerator, _broadcaster, _logger);
    }

    private ChatSession ActiveSession() =>
        new()
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };

    [Fact]
    public async Task RunAsync_TitleGenSucceeds_SavesGeneratedTitle()
    {
        var session = ActiveSession();
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _titleGenerator
            .GenerateTitleAsync(UserId, "hello", "hi there", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Greeting Exchange"));

        await _job.RunAsync(SessionId, UserId, "hello", "hi there", CancellationToken.None);

        await _sessionRepo
            .Received(1)
            .UpdateAsync(
                Arg.Is<ChatSession>(s => s.Title == "Greeting Exchange"),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunAsync_TitleSaved_BroadcastsSessionUpdatedToConnectedClients()
    {
        // This job runs decoupled from the chat reply's own request — without this push,
        // a client connected right now has no way to learn the title changed short of a
        // manual refresh (the bug this closes).
        var session = ActiveSession();
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _titleGenerator
            .GenerateTitleAsync(UserId, "hello", "hi there", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Greeting Exchange"));

        await _job.RunAsync(SessionId, UserId, "hello", "hi there", CancellationToken.None);

        await _mockCaller.Received(1).SessionUpdated(SessionId, "Greeting Exchange", "Active");
    }

    [Fact]
    public async Task RunAsync_FallbackTitleSaved_AlsoBroadcastsSessionUpdated()
    {
        var session = ActiveSession();
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _titleGenerator
            .GenerateTitleAsync(UserId, "hello", "hi there", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new Error("Chat.TitleFailed", "Simulated failure")));

        await _job.RunAsync(SessionId, UserId, "hello", "hi there", CancellationToken.None);

        await _mockCaller.Received(1).SessionUpdated(SessionId, "hello", "Active");
    }

    [Fact]
    public async Task RunAsync_TitleGenFails_FallsBackToFirstUserMessage()
    {
        var session = ActiveSession();
        const string userMessage = "Hello world this is a test message";
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _titleGenerator
            .GenerateTitleAsync(UserId, userMessage, "reply", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new Error("Chat.TitleFailed", "Simulated failure")));

        await _job.RunAsync(SessionId, UserId, userMessage, "reply", CancellationToken.None);

        await _sessionRepo
            .Received(1)
            .UpdateAsync(
                Arg.Is<ChatSession>(s => s.Title == userMessage),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunAsync_TitleGenFails_TruncatesLongFirstMessage()
    {
        var longContent = string.Join(" ", Enumerable.Range(0, 20).Select(i => $"word{i}"));
        var expectedTruncatedTitle = longContent[..60] + "…";
        var session = ActiveSession();
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _titleGenerator
            .GenerateTitleAsync(UserId, longContent, "reply", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new Error("Chat.TitleFailed", "Simulated failure")));

        await _job.RunAsync(SessionId, UserId, longContent, "reply", CancellationToken.None);

        await _sessionRepo
            .Received(1)
            .UpdateAsync(
                Arg.Is<ChatSession>(s => s.Title == expectedTruncatedTitle),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunAsync_SessionNotFound_DoesNotCallTitleGeneratorOrUpdate()
    {
        _sessionRepo
            .GetByIdAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns((ChatSession?)null);

        await _job.RunAsync(SessionId, UserId, "hello", "hi", CancellationToken.None);

        await _titleGenerator
            .DidNotReceive()
            .GenerateTitleAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
        await _sessionRepo
            .DidNotReceive()
            .UpdateAsync(Arg.Any<ChatSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_SessionClosedSinceEnqueue_SkipsWithoutError()
    {
        // The job is queued independently of the reply job — by the time it runs, the
        // session could have been closed. Nothing meaningful to title then.
        var session = ActiveSession();
        session.Close(null);
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);

        await _job.RunAsync(SessionId, UserId, "hello", "hi", CancellationToken.None);

        await _titleGenerator
            .DidNotReceive()
            .GenerateTitleAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunAsync_TitleGeneratorThrows_LogsWarningAndDoesNotThrow()
    {
        var session = ActiveSession();
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _titleGenerator
            .GenerateTitleAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns<Result<string>>(_ => throw new InvalidOperationException("boom"));

        // Must not throw — a flaky LLM call is not allowed to fail this Hangfire job.
        await _job.RunAsync(SessionId, UserId, "hello", "hi", CancellationToken.None);

        await _sessionRepo
            .DidNotReceive()
            .UpdateAsync(Arg.Any<ChatSession>(), Arg.Any<CancellationToken>());
    }
}
