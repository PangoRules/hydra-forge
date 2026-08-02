namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using DomainChatMessage = HydraForge.Domain.Entities.Chat.ChatMessage;
using DomainChatSession = HydraForge.Domain.Entities.Chat.ChatSession;
using DomainChatSessionDocument = HydraForge.Domain.Entities.Chat.ChatSessionDocument;
using DomainPersonality = HydraForge.Domain.Entities.PersonalSpace.AgentPersonality;
using DomainDocumentChunk = HydraForge.Domain.Entities.PersonalSpace.DocumentChunk;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;

public class ChatRagRetrieverTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid PersonalityId = Guid.NewGuid();
    private static readonly Guid DocId = Guid.NewGuid();

    private static (ChatRagRetriever, TestDeps) CreateSut()
    {
        var sessionRepo = Substitute.For<IChatSessionRepository>();
        var messageRepo = Substitute.For<IChatMessageRepository>();
        var sessionDocRepo = Substitute.For<IChatSessionDocumentRepository>();
        var personalityRepo = Substitute.For<IAgentPersonalityRepository>();
        var chunkRepo = Substitute.For<IDocumentChunkRepository>();
        var snapshotRepo = Substitute.For<IProjectContextSnapshotRepository>();
        var modelRouter = Substitute.For<IModelRouter>();
        var clientFactory = Substitute.For<ILlmClientFactory>();
        var embeddingClient = Substitute.For<IEmbeddingClient>();
        var logger = Substitute.For<ILogger<ChatRagRetriever>>();

        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            AdapterType = AdapterType.OpenAiCompatible,
        };

        var configDto = new ProviderModelConfigDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "text-embedding-3-small",
            "Embedding",
            "Standard",
            0m,
            1536,
            true
        );

        var providerDto = new ProviderDto(
            Guid.NewGuid(),
            "OpenAI",
            "https://api.openai.com",
            "OpenAiCompatible",
            "OpenAI",
            "Standard",
            null,
            true,
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var routeDecision = new RouteDecision(configDto, providerDto, [], provider);
        modelRouter
            .ResolveAsync(
                AiFeature.DocumentEmbedding,
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<RouteDecision>.Success(routeDecision));

        clientFactory.EmbeddingFor(provider).Returns(embeddingClient);

        embeddingClient
            .EmbedAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingResult>.Success(new EmbeddingResult(
                [(ReadOnlyMemory<float>)new float[1536]])));

        var retriever = new ChatRagRetriever(
            modelRouter,
            clientFactory,
            sessionRepo,
            messageRepo,
            sessionDocRepo,
            personalityRepo,
            chunkRepo,
            snapshotRepo,
            logger
        );

        var deps = new TestDeps(
            sessionRepo,
            messageRepo,
            sessionDocRepo,
            personalityRepo,
            chunkRepo,
            snapshotRepo,
            modelRouter,
            clientFactory,
            embeddingClient,
            logger
        );

        return (retriever, deps);
    }

    private record TestDeps(
        IChatSessionRepository SessionRepo,
        IChatMessageRepository MessageRepo,
        IChatSessionDocumentRepository SessionDocRepo,
        IAgentPersonalityRepository PersonalityRepo,
        IDocumentChunkRepository ChunkRepo,
        IProjectContextSnapshotRepository SnapshotRepo,
        IModelRouter ModelRouter,
        ILlmClientFactory ClientFactory,
        IEmbeddingClient EmbeddingClient,
        ILogger<ChatRagRetriever> Logger
    );

    [Fact]
    public async Task RetrieveAsync_SessionNotFound_ReturnsEmptyBlocks()
    {
        var (retriever, deps) = CreateSut();
        deps.SessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns((ChatSession?)null);

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: false, null, k: 5);

        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveAsync_SearchAllMyDocsFalse_CallsSearchWithSpecificDocIds()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false);
        SetupSessionFound(deps, session);

        var sessionDocs = new List<DomainChatSessionDocument>
        {
            new() { SessionId = SessionId, DocumentId = DocId }
        };
        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(sessionDocs);

        var chunks = new List<DocumentChunk>
        {
            new() { Id = Guid.NewGuid(), Content = "chunk1", ChunkIndex = 0 },
            new() { Id = Guid.NewGuid(), Content = "chunk2", ChunkIndex = 1 }
        };
        deps.ChunkRepo.SearchAsync(
                UserId,
                Arg.Is<IReadOnlyList<Guid>>(ids => ids != null && ids.Count == 1 && ids[0] == DocId),
                Arg.Any<ReadOnlyMemory<float>>(),
                5,
                Arg.Any<CancellationToken>())
            .Returns(chunks);

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: false, null, k: 5);

        Assert.Single(result);
        Assert.Equal(CacheBlockType.SystemContext, result[0].Type);
    }

    [Fact]
    public async Task RetrieveAsync_SearchAllMyDocsTrue_CallsSearchWithNullDocIds()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: true);
        SetupSessionFound(deps, session);

        deps.ChunkRepo.SearchAsync(
                UserId,
                Arg.Is<IReadOnlyList<Guid>?>(ids => ids == null),
                Arg.Any<ReadOnlyMemory<float>>(),
                5,
                Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: true, null, k: 5);

        Assert.Single(result);
    }

    [Fact]
    public async Task RetrieveAsync_EmbeddingFails_ReturnsEmptyBlocks()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: true);
        SetupSessionFound(deps, session);

        deps.EmbeddingClient
            .EmbedAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingResult>.Failure(new Error("FAIL", "embedding failed")));

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: true, null, k: 5);

        Assert.Empty(result);
        deps.Logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("RAG embedding failed")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RetrieveAsync_NoPriorMessages_IncludesProjectSnapshot()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: ProjectId);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatSessionDocument> { new() { SessionId = SessionId, DocumentId = DocId } });

        deps.MessageRepo.GetBySessionAsync(SessionId, null, 1, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatMessage>());

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        deps.SnapshotRepo.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectContextSnapshot { TemplateContent = "snapshot content" });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: false, null, k: 5);

        Assert.Equal(2, result.Count);
        Assert.Equal(CacheBlockType.ProjectSnapshot, result[0].Type);
        Assert.Equal("snapshot content", result[0].Content);
    }

    [Fact]
    public async Task RetrieveAsync_PriorMessagesExist_NoProjectSnapshot()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: ProjectId);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatSessionDocument> { new() { SessionId = SessionId, DocumentId = DocId } });

        deps.MessageRepo.GetBySessionAsync(SessionId, null, 1, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatMessage> { new() { Content = "prior" } });

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: false, null, k: 5);

        Assert.Single(result);
        Assert.NotEqual(CacheBlockType.ProjectSnapshot, result[0].Type);
    }

    [Fact]
    public async Task RetrieveAsync_ActivePersonality_IncludesInBlocks()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, personalityId: PersonalityId);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatSessionDocument> { new() { SessionId = SessionId, DocumentId = DocId } });

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        deps.PersonalityRepo.GetByIdAsync(PersonalityId, Arg.Any<CancellationToken>())
            .Returns(new AgentPersonality
            {
                Id = PersonalityId,
                Name = "TestBot",
                SystemPrompt = "You are a helpful bot.",
                ArchivedAt = null
            });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: false, null, k: 5);

        Assert.Equal(2, result.Count);
        Assert.Equal(CacheBlockType.SystemContext, result[0].Type);
        Assert.Equal("You are a helpful bot.", result[0].Content);
    }

    [Fact]
    public async Task RetrieveAsync_ArchivedPersonality_NotIncluded()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, personalityId: PersonalityId);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatSessionDocument> { new() { SessionId = SessionId, DocumentId = DocId } });

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        deps.PersonalityRepo.GetByIdAsync(PersonalityId, Arg.Any<CancellationToken>())
            .Returns(new AgentPersonality
            {
                Id = PersonalityId,
                Name = "OldBot",
                SystemPrompt = "You are old.",
                ArchivedAt = DateTime.UtcNow.AddDays(-1)
            });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: false, null, k: 5);

        Assert.Single(result);
        Assert.NotEqual("You are old.", result[0].Content);
    }

    [Fact]
    public async Task RetrieveAsync_NoPersonality_NoPersonalityBlock()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, personalityId: null);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatSessionDocument> { new() { SessionId = SessionId, DocumentId = DocId } });

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: false, null, k: 5);

        Assert.Single(result);
    }

    [Fact]
    public async Task RetrieveAsync_PresetContent_WrappedInTags()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: true);
        SetupSessionFound(deps, session);

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: true, "my preset content", k: 5);

        Assert.Equal(2, result.Count);
        var presetBlock = result.Single(b => b.Content.Contains("<preset>"));
        Assert.Contains("my preset content", presetBlock.Content);
        Assert.StartsWith("<preset>", presetBlock.Content);
        Assert.EndsWith("</preset>", presetBlock.Content);
    }

    [Fact]
    public async Task RetrieveAsync_NullPreset_NoPresetBlock()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: true);
        SetupSessionFound(deps, session);

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: true, null, k: 5);

        Assert.Single(result);
        Assert.DoesNotContain(result, b => b.Content.Contains("<preset>"));
    }

    [Fact]
    public async Task RetrieveAsync_HappyPath_ReturnsCorrectBlockStructure()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: ProjectId, personalityId: PersonalityId);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatSessionDocument> { new() { SessionId = SessionId, DocumentId = DocId } });

        deps.MessageRepo.GetBySessionAsync(SessionId, null, 1, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatMessage>());

        deps.SnapshotRepo.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectContextSnapshot { TemplateContent = "snapshot" });

        deps.PersonalityRepo.GetByIdAsync(PersonalityId, Arg.Any<CancellationToken>())
            .Returns(new AgentPersonality { Id = PersonalityId, Name = "Bot", SystemPrompt = "prompt", ArchivedAt = null });

        var chunks = new List<DocumentChunk>
        {
            new() { Id = Guid.NewGuid(), Content = "chunk A", ChunkIndex = 0 },
            new() { Id = Guid.NewGuid(), Content = "chunk B", ChunkIndex = 1 }
        };
        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<ReadOnlyMemory<float>>(), 5, Arg.Any<CancellationToken>())
            .Returns(chunks);

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: false, "my preset", k: 5);

        Assert.Equal(4, result.Count);
        // Personality inserted at index 0 first, then snapshot inserted at 0 shifts it to 1
        Assert.Equal(CacheBlockType.SystemContext, result[0].Type);
        Assert.Equal("prompt", result[0].Content);
        Assert.Equal(CacheBlockType.ProjectSnapshot, result[1].Type);
        Assert.Equal("snapshot", result[1].Content);
        Assert.Equal(CacheBlockType.SystemContext, result[2].Type);
        Assert.Contains("chunk A", result[2].Content);
        Assert.Contains("chunk B", result[2].Content);
        Assert.Contains("<preset>", result[3].Content);
    }

    private static ChatSession CreateSession(bool searchAllMyDocs, Guid? projectId = null, Guid? personalityId = null)
    {
        return new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            SearchAllMyDocs = searchAllMyDocs,
            ProjectId = projectId,
            PersonalityId = personalityId,
        };
    }

    private static void SetupSessionFound(TestDeps deps, ChatSession session)
    {
        deps.SessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(session);
    }
}
