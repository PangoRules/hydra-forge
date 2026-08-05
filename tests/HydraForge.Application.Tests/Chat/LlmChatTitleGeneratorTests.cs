using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HydraForge.Application.Tests.Chat;

public class LlmChatTitleGeneratorTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IModelRouter _modelRouter;
    private readonly ILlmClientFactory _clientFactory;
    private readonly IUsageRecorder _usageRecorder;
    private readonly ILogger<LlmChatTitleGenerator> _logger;
    private readonly LlmChatTitleGenerator _generator;

    public LlmChatTitleGeneratorTests()
    {
        _modelRouter = Substitute.For<IModelRouter>();
        _clientFactory = Substitute.For<ILlmClientFactory>();
        _usageRecorder = Substitute.For<IUsageRecorder>();
        _logger = Substitute.For<ILogger<LlmChatTitleGenerator>>();
        _generator = new LlmChatTitleGenerator(
            _modelRouter,
            _clientFactory,
            _usageRecorder,
            _logger
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
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<RouteDecision>.Success(routeDecision));
    }

    private ILlmClient SetLlmResponse(string content)
    {
        var mockClient = Substitute.For<ILlmClient>();
        mockClient
            .StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeImmediateEnumerable(content));
        _clientFactory.For(Arg.Any<LlmProvider>()).Returns(mockClient);
        return mockClient;
    }

    private ILlmClient SetLlmResponseSequence(params string[] contents)
    {
        var mockClient = Substitute.For<ILlmClient>();
        mockClient
            .StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                MakeImmediateEnumerable(contents[0]),
                contents.Skip(1).Select(MakeImmediateEnumerable).ToArray()
            );
        _clientFactory.For(Arg.Any<LlmProvider>()).Returns(mockClient);
        return mockClient;
    }

    [Fact]
    public async Task GenerateTitleAsync_ModelEchoesUserMessageVerbatim_ReturnsFailure()
    {
        // Observed with both cloud (DeepSeek) and local (gemma4:26b) models: the LLM
        // ignores the "generate a short title" instruction and just repeats the
        // input back. That's a 200 OK with real content — IsSuccess alone can't
        // catch it, so this must be rejected as a quality check, not a call failure.
        const string userMessage = "I need an introduction of who you are and what we can do";
        SetLlmResponse(userMessage);

        var result = await _generator.GenerateTitleAsync(
            UserId,
            userMessage,
            "some assistant reply"
        );

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GenerateTitleAsync_ModelReturnsFullSentenceFarBeyondRequestedLength_ReturnsFailure()
    {
        const string userMessage = "Hello!";
        SetLlmResponse(
            "This is a very long response that goes on and on and completely ignores the instruction to keep it short"
        );

        var result = await _generator.GenerateTitleAsync(
            UserId,
            userMessage,
            "some assistant reply"
        );

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GenerateTitleAsync_ModelReturnsProperShortTitle_ReturnsSuccess()
    {
        const string userMessage = "Hello, testing title generation";
        SetLlmResponse("Testing Title Generation");

        var result = await _generator.GenerateTitleAsync(
            UserId,
            userMessage,
            "some assistant reply"
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("Testing Title Generation", result.Value);
    }

    [Fact]
    public async Task GenerateTitleAsync_ShortMessageWithShortDistinctTitle_DoesNotFalselyRejectOnLength()
    {
        // A short user message paired with a short-but-different title must not be
        // rejected just because the title's length is close to the message's length
        // — only a genuine echo or an over-long response should fail.
        const string userMessage = "Hi there";
        SetLlmResponse("Greeting");

        var result = await _generator.GenerateTitleAsync(
            UserId,
            userMessage,
            "some assistant reply"
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("Greeting", result.Value);
    }

    [Fact]
    public async Task GenerateTitleAsync_RoutingFails_ReturnsFailure()
    {
        _modelRouter
            .ResolveAsync(
                Arg.Any<AiFeature>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<RouteDecision>.Failure(new Error("NO_MODEL", "no model configured")));

        var result = await _generator.GenerateTitleAsync(UserId, "hello", "hi");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GenerateTitleAsync_RoutingFails_DoesNotRetryRouting()
    {
        // Routing failure is a config problem (no model enabled for the feature), not
        // a transient LLM hiccup — retrying it wastes calls and can't change the outcome.
        _modelRouter
            .ResolveAsync(
                Arg.Any<AiFeature>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<RouteDecision>.Failure(new Error("NO_MODEL", "no model configured")));

        await _generator.GenerateTitleAsync(UserId, "hello", "hi");

        await _modelRouter
            .Received(1)
            .ResolveAsync(
                Arg.Any<AiFeature>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GenerateTitleAsync_FailsTwiceThenSucceeds_RetriesAndReturnsTheSuccessfulTitle()
    {
        // Reasoning models intermittently burn their token budget on hidden thinking
        // and return empty content — this should be treated as a retryable hiccup,
        // not an immediate fall-through to the caller's crop-fallback.
        const string userMessage = "Hello, testing retry behavior";
        var mockClient = SetLlmResponseSequence("", "", "Retry Behavior Test");

        var result = await _generator.GenerateTitleAsync(
            UserId,
            userMessage,
            "some assistant reply"
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("Retry Behavior Test", result.Value);
        mockClient
            .Received(3)
            .StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateTitleAsync_FailsAllAttempts_ReturnsFailureAfterExactlyThreeTries()
    {
        const string userMessage = "Hello, testing exhausted retries";
        var mockClient = SetLlmResponse(""); // empty every attempt

        var result = await _generator.GenerateTitleAsync(
            UserId,
            userMessage,
            "some assistant reply"
        );

        Assert.True(result.IsFailure);
        mockClient
            .Received(3)
            .StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<ChatChunk> MakeImmediateEnumerable(string content)
    {
        yield return new ChatChunk(content, null, null);
        await Task.CompletedTask;
    }
}
