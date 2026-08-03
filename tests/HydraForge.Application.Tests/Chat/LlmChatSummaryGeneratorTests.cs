namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ChatMessage = HydraForge.Domain.Entities.Chat.ChatMessage;
using DomainErrorCodes = HydraForge.Domain.Common.DomainErrorCodes;
using MessageRole = HydraForge.Domain.Enums.MessageRole;

public class LlmChatSummaryGeneratorTests
{
    private static Guid NewId() => Guid.NewGuid();

    private sealed class FakeSessionRepo : IChatSessionRepository
    {
        public List<ChatSession> Sessions { get; } = [];

        public Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

        public Task<ChatSession?> GetActiveByPanelAsync(
            Guid projectId, Guid? openCardId, Guid ownerId, CancellationToken ct = default
        ) => Task.FromResult<ChatSession?>(null);

        public Task<IReadOnlyList<ChatSession>> ListAsync(
            Guid ownerId, Guid? folderId, Guid? projectId, DateTime? before, int limit, CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatSession session, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
            Guid ownerId, string query, Guid? projectId, int limit, CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeMessageRepo : IChatMessageRepository
    {
        public List<ChatMessage> Messages { get; } = [];

        public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.Id == messageId));

        public Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
            Guid sessionId, DateTime? before, Guid? beforeId, int limit, CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatMessage>>(
            Messages.Where(m => m.SessionId == sessionId).Take(limit).ToList()
        );

        public Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
            Guid ownerId, string query, Guid? projectId, int limit, CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

        public Task AddAsync(ChatMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUsageRecorder : IUsageRecorder
    {
        public Task RecordTokenAsync(TokenUsageRecordInput input, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RecordTokenBatchAsync(IReadOnlyList<TokenUsageRecordInput> inputs, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RecordImageAsync(ImageUsageRecordInput input, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<int> AccrueTokenUsageAsync(Guid userId, int tokens, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<int> AccrueImageUsageAsync(Guid userId, int count, CancellationToken ct = default) =>
            Task.FromResult(0);
    }

    private static LlmChatSummaryGenerator CreateGenerator(
        FakeSessionRepo sessionRepo,
        FakeMessageRepo messageRepo,
        IModelRouter router,
        ILlmClientFactory clientFactory,
        IUsageRecorder usageRecorder
    ) =>
        new(
            sessionRepo,
            messageRepo,
            router,
            clientFactory,
            usageRecorder,
            Substitute.For<ILogger<LlmChatSummaryGenerator>>()
        );

    [Fact]
    async Task GenerateSummaryAsync_SuccessfulSummary_ReturnsSummaryString()
    {
        // Arrange
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo();
        var router = Substitute.For<IModelRouter>();
        var clientFactory = Substitute.For<ILlmClientFactory>();
        var usageRecorder = new FakeUsageRecorder();

        var ownerId = NewId();
        var sessionId = NewId();
        var providerId = NewId();
        var configId = NewId();

        var session = new ChatSession { Id = sessionId, OwnerId = ownerId };
        sessionRepo.Sessions.Add(session);

        messageRepo.Messages.Add(new ChatMessage { Id = NewId(), SessionId = sessionId, Role = MessageRole.User, Content = "Hello" });
        messageRepo.Messages.Add(new ChatMessage { Id = NewId(), SessionId = sessionId, Role = MessageRole.Assistant, Content = "Hi there" });

        var modelConfig = new ProviderModelConfigDto(configId, providerId, "gpt-4o", "GPT-4o", "Standard", 1m, 4096, true);
        var provider = new ProviderDto(providerId, "OpenAI", "https://api.openai.com", "OpenAi", "OpenAI", "Standard", null, true, DateTime.UtcNow, DateTime.UtcNow);
        var routeDecision = new RouteDecision(modelConfig, provider, [], null);

        router.ResolveAsync(AiFeature.PersonalChat, ownerId, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDecision>.Success(routeDecision));

        var llmClient = Substitute.For<ILlmClient>();
        var chunks = new[]
        {
            new ChatChunk("This ", null, null),
            new ChatChunk("is a ", null, null),
            new ChatChunk("summary.", null, new UsageSnapshot(10, 5, 0)),
            new ChatChunk(null, ChatChunkFinishReason.Stop, new UsageSnapshot(10, 5, 0)),
        }.ToAsyncEnumerable();

        llmClient.StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        clientFactory.For(Arg.Any<LlmProvider>()).Returns(llmClient);

        var sut = CreateGenerator(sessionRepo, messageRepo, router, clientFactory, usageRecorder);

        // Act
        var result = await sut.GenerateSummaryAsync(sessionId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("This is a summary.", result.Value);
    }

    [Fact]
    async Task GenerateSummaryAsync_EmptyMessages_ReturnsEmptyString()
    {
        // Arrange
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo();
        var router = Substitute.For<IModelRouter>();
        var clientFactory = Substitute.For<ILlmClientFactory>();
        var usageRecorder = new FakeUsageRecorder();

        var ownerId = NewId();
        var sessionId = NewId();

        var session = new ChatSession { Id = sessionId, OwnerId = ownerId };
        sessionRepo.Sessions.Add(session);
        // No messages added — list is empty

        var sut = CreateGenerator(sessionRepo, messageRepo, router, clientFactory, usageRecorder);

        // Act
        var result = await sut.GenerateSummaryAsync(sessionId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value);
        await router.Received(0).ResolveAsync(Arg.Any<AiFeature>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    async Task GenerateSummaryAsync_RouterFailure_ReturnsFailure()
    {
        // Arrange
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo();
        var router = Substitute.For<IModelRouter>();
        var clientFactory = Substitute.For<ILlmClientFactory>();
        var usageRecorder = new FakeUsageRecorder();

        var ownerId = NewId();
        var sessionId = NewId();

        var session = new ChatSession { Id = sessionId, OwnerId = ownerId };
        sessionRepo.Sessions.Add(session);

        messageRepo.Messages.Add(new ChatMessage { Id = NewId(), SessionId = sessionId, Role = MessageRole.User, Content = "Hello" });

        router.ResolveAsync(AiFeature.PersonalChat, ownerId, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDecision>.Failure(new Error("ROUTING_ERROR", "Routing failed.")));

        var sut = CreateGenerator(sessionRepo, messageRepo, router, clientFactory, usageRecorder);

        // Act
        var result = await sut.GenerateSummaryAsync(sessionId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SummaryFailed, result.Error.Code);
    }

    [Fact]
    async Task GenerateSummaryAsync_StreamErrorFinishReason_ReturnsFailure()
    {
        // Arrange
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo();
        var router = Substitute.For<IModelRouter>();
        var clientFactory = Substitute.For<ILlmClientFactory>();
        var usageRecorder = new FakeUsageRecorder();

        var ownerId = NewId();
        var sessionId = NewId();
        var providerId = NewId();
        var configId = NewId();

        var session = new ChatSession { Id = sessionId, OwnerId = ownerId };
        sessionRepo.Sessions.Add(session);

        messageRepo.Messages.Add(new ChatMessage { Id = NewId(), SessionId = sessionId, Role = MessageRole.User, Content = "Hello" });

        var modelConfig = new ProviderModelConfigDto(configId, providerId, "gpt-4o", "GPT-4o", "Standard", 1m, 4096, true);
        var provider = new ProviderDto(providerId, "OpenAI", "https://api.openai.com", "OpenAi", "OpenAI", "Standard", null, true, DateTime.UtcNow, DateTime.UtcNow);
        var routeDecision = new RouteDecision(modelConfig, provider, [], null);

        router.ResolveAsync(AiFeature.PersonalChat, ownerId, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDecision>.Success(routeDecision));

        var llmClient = Substitute.For<ILlmClient>();
        var chunks = new[]
        {
            new ChatChunk("partial ", null, null),
            new ChatChunk(null, ChatChunkFinishReason.Error, null),
        }.ToAsyncEnumerable();

        llmClient.StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        clientFactory.For(Arg.Any<LlmProvider>()).Returns(llmClient);

        var sut = CreateGenerator(sessionRepo, messageRepo, router, clientFactory, usageRecorder);

        // Act
        var result = await sut.GenerateSummaryAsync(sessionId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SummaryFailed, result.Error.Code);
    }

    [Fact]
    async Task GenerateSummaryAsync_ContentFilter_ReturnsFailure()
    {
        // Arrange
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo();
        var router = Substitute.For<IModelRouter>();
        var clientFactory = Substitute.For<ILlmClientFactory>();
        var usageRecorder = new FakeUsageRecorder();

        var ownerId = NewId();
        var sessionId = NewId();
        var providerId = NewId();
        var configId = NewId();

        var session = new ChatSession { Id = sessionId, OwnerId = ownerId };
        sessionRepo.Sessions.Add(session);

        messageRepo.Messages.Add(new ChatMessage { Id = NewId(), SessionId = sessionId, Role = MessageRole.User, Content = "Hello" });

        var modelConfig = new ProviderModelConfigDto(configId, providerId, "gpt-4o", "GPT-4o", "Standard", 1m, 4096, true);
        var provider = new ProviderDto(providerId, "OpenAI", "https://api.openai.com", "OpenAi", "OpenAI", "Standard", null, true, DateTime.UtcNow, DateTime.UtcNow);
        var routeDecision = new RouteDecision(modelConfig, provider, [], null);

        router.ResolveAsync(AiFeature.PersonalChat, ownerId, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDecision>.Success(routeDecision));

        var llmClient = Substitute.For<ILlmClient>();
        var chunks = new[]
        {
            new ChatChunk(null, ChatChunkFinishReason.ContentFilter, null),
        }.ToAsyncEnumerable();

        llmClient.StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        clientFactory.For(Arg.Any<LlmProvider>()).Returns(llmClient);

        var sut = CreateGenerator(sessionRepo, messageRepo, router, clientFactory, usageRecorder);

        // Act
        var result = await sut.GenerateSummaryAsync(sessionId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SummaryFailed, result.Error.Code);
    }
}
