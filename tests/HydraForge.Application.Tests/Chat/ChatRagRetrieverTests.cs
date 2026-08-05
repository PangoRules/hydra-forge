namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using DomainChatMessage = HydraForge.Domain.Entities.Chat.ChatMessage;
using DomainChatSession = HydraForge.Domain.Entities.Chat.ChatSession;
using DomainChatSessionDocument = HydraForge.Domain.Entities.Chat.ChatSessionDocument;
using DomainDocumentChunk = HydraForge.Domain.Entities.PersonalSpace.DocumentChunk;

public class ChatRagRetrieverTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid DocId = Guid.NewGuid();

    private static (ChatRagRetriever, TestDeps) CreateSut()
    {
        var sessionRepo = Substitute.For<IChatSessionRepository>();
        var sessionDocRepo = Substitute.For<IChatSessionDocumentRepository>();
        var messageRepo = Substitute.For<IChatMessageRepository>();
        var chunkRepo = Substitute.For<IDocumentChunkRepository>();
        var snapshotRepo = Substitute.For<IProjectContextSnapshotRepository>();
        var modelRouter = Substitute.For<IModelRouter>();
        var clientFactory = Substitute.For<ILlmClientFactory>();
        var embeddingClient = Substitute.For<IEmbeddingClient>();
        var logger = Substitute.For<ILogger<ChatRagRetriever>>();
        var ragOptions = Substitute.For<IOptions<RagOptions>>();
        ragOptions.Value.Returns(new RagOptions { TopK = 8 });

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
            .Returns(
                Result<EmbeddingResult>.Success(
                    new EmbeddingResult([(ReadOnlyMemory<float>)new float[1536]])
                )
            );

        var retriever = new ChatRagRetriever(
            modelRouter,
            clientFactory,
            sessionRepo,
            sessionDocRepo,
            messageRepo,
            chunkRepo,
            snapshotRepo,
            ragOptions,
            logger
        );

        var deps = new TestDeps(
            sessionRepo,
            sessionDocRepo,
            messageRepo,
            chunkRepo,
            snapshotRepo,
            modelRouter,
            clientFactory,
            embeddingClient,
            ragOptions,
            logger
        );

        return (retriever, deps);
    }

    private record TestDeps(
        IChatSessionRepository SessionRepo,
        IChatSessionDocumentRepository SessionDocRepo,
        IChatMessageRepository MessageRepo,
        IDocumentChunkRepository ChunkRepo,
        IProjectContextSnapshotRepository SnapshotRepo,
        IModelRouter ModelRouter,
        ILlmClientFactory ClientFactory,
        IEmbeddingClient EmbeddingClient,
        IOptions<RagOptions> RagOptions,
        ILogger<ChatRagRetriever> Logger
    );

    [Fact]
    public async Task RetrieveAsync_SessionNotFound_ReturnsEmptyBlocks()
    {
        var (retriever, deps) = CreateSut();
        deps.SessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns((ChatSession?)null);

        var result = await retriever.RetrieveAsync(
            SessionId,
            "query",
            searchAllMyDocs: false,
            k: 5
        );

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
            new() { SessionId = SessionId, DocumentId = DocId },
        };
        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(sessionDocs);

        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "chunk1",
                ChunkIndex = 0,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Content = "chunk2",
                ChunkIndex = 1,
            },
        };
        deps.ChunkRepo.SearchAsync(
                UserId,
                Arg.Is<IReadOnlyList<Guid>>(ids =>
                    ids != null && ids.Count == 1 && ids[0] == DocId
                ),
                Arg.Any<ReadOnlyMemory<float>>(),
                5,
                Arg.Any<CancellationToken>()
            )
            .Returns(chunks);

        var result = await retriever.RetrieveAsync(
            SessionId,
            "query",
            searchAllMyDocs: false,
            k: 5
        );

        Assert.Single(result);
        Assert.Equal(CacheBlockType.RagContext, result[0].Type);
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
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: true, k: 5);

        Assert.Single(result);
    }

    [Fact]
    public async Task RetrieveAsync_EmbeddingFails_ReturnsEmptyBlocks()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: true);
        SetupSessionFound(deps, session);

        deps.EmbeddingClient.EmbedAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingResult>.Failure(new Error("FAIL", "embedding failed")));

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: true, k: 5);

        Assert.Empty(result);
        deps.Logger.Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("RAG embedding failed")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task RetrieveAsync_EmbeddingReturnsEmptyVectors_ReturnsEmptyBlocks()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: true);
        SetupSessionFound(deps, session);

        deps.EmbeddingClient.EmbedAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingResult>.Success(new EmbeddingResult([])));

        var result = await retriever.RetrieveAsync(SessionId, "query", searchAllMyDocs: true, k: 5);

        Assert.Empty(result);
        await deps
            .ChunkRepo.DidNotReceive()
            .SearchAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RetrieveAsync_ProjectChat_IncludesProjectSnapshot()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: ProjectId);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(
                new List<DomainChatSessionDocument>
                {
                    new() { SessionId = SessionId, DocumentId = DocId },
                }
            );

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        deps.SnapshotRepo.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectContextSnapshot { TemplateContent = "snapshot content" });

        var result = await retriever.RetrieveAsync(
            SessionId,
            "query",
            searchAllMyDocs: false,
            k: 5
        );

        Assert.Equal(2, result.Count);
        Assert.Equal(CacheBlockType.ProjectSnapshot, result[0].Type);
        Assert.Equal("snapshot content", result[0].Content);
    }

    [Fact]
    public async Task RetrieveAsync_NonProjectChat_NoSnapshot()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: null);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(
                new List<DomainChatSessionDocument>
                {
                    new() { SessionId = SessionId, DocumentId = DocId },
                }
            );

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        var result = await retriever.RetrieveAsync(
            SessionId,
            "query",
            searchAllMyDocs: false,
            k: 5
        );

        Assert.Single(result);
        Assert.Equal(CacheBlockType.RagContext, result[0].Type);
    }

    [Fact]
    public async Task RetrieveAsync_NoSessionDocs_ReturnsSnapshotOnly()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: ProjectId);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatSessionDocument>());

        deps.SnapshotRepo.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectContextSnapshot { TemplateContent = "snapshot content" });

        var result = await retriever.RetrieveAsync(
            SessionId,
            "query",
            searchAllMyDocs: false,
            k: 5
        );

        Assert.Single(result);
        Assert.Equal(CacheBlockType.ProjectSnapshot, result[0].Type);
        Assert.Equal("snapshot content", result[0].Content);
    }

    [Fact]
    public async Task RetrieveAsync_NotFirstMessage_OmitsSnapshot()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: ProjectId);
        SetupSessionFound(deps, session);

        deps.MessageRepo.GetBySessionAsync(
                SessionId,
                Arg.Any<DateTime?>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new List<DomainChatMessage>
                {
                    new() { Id = Guid.NewGuid(), Role = MessageRole.User },
                }
            );

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(
                new List<DomainChatSessionDocument>
                {
                    new() { SessionId = SessionId, DocumentId = DocId },
                }
            );

        deps.SnapshotRepo.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectContextSnapshot { TemplateContent = "snapshot content" });

        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<DocumentChunk> { new() { Content = "chunk1" } });

        var result = await retriever.RetrieveAsync(
            SessionId,
            "query",
            searchAllMyDocs: false,
            k: 5
        );

        Assert.Single(result);
        Assert.Equal(CacheBlockType.RagContext, result[0].Type);
        Assert.DoesNotContain(result, b => b.Type == CacheBlockType.ProjectSnapshot);
    }

    [Fact]
    public async Task RetrieveAsync_HappyPath_ReturnsCorrectBlockStructure()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: ProjectId);
        SetupSessionFound(deps, session);

        deps.SessionDocRepo.GetBySessionAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(
                new List<DomainChatSessionDocument>
                {
                    new() { SessionId = SessionId, DocumentId = DocId },
                }
            );

        deps.SnapshotRepo.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectContextSnapshot { TemplateContent = "snapshot" });

        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "chunk A",
                ChunkIndex = 0,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Content = "chunk B",
                ChunkIndex = 1,
            },
        };
        deps.ChunkRepo.SearchAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<ReadOnlyMemory<float>>(),
                5,
                Arg.Any<CancellationToken>()
            )
            .Returns(chunks);

        var result = await retriever.RetrieveAsync(
            SessionId,
            "query",
            searchAllMyDocs: false,
            k: 5
        );

        Assert.Equal(2, result.Count);
        Assert.Equal(CacheBlockType.ProjectSnapshot, result[0].Type);
        Assert.Equal("snapshot", result[0].Content);
        Assert.Equal(CacheBlockType.RagContext, result[1].Type);
        Assert.Contains("chunk A", result[1].Content);
        Assert.Contains("chunk B", result[1].Content);
    }

    [Fact]
    public async Task RetrieveAsync_OnlyIdentitySystemMessagePresent_StillInjectsProjectSnapshot()
    {
        var (retriever, deps) = CreateSut();
        var session = CreateSession(searchAllMyDocs: false, projectId: ProjectId);
        SetupSessionFound(deps, session);

        // Only the identity System message is present (first real user message not yet sent)
        deps.MessageRepo
            .GetBySessionAsync(SessionId, null, null, 1, Arg.Any<CancellationToken>())
            .Returns(new List<DomainChatMessage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SessionId = SessionId,
                    Role = MessageRole.System,
                    Content = "You are HydraForge's assistant.",
                }
            });

        deps.SnapshotRepo.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectContextSnapshot { TemplateContent = "snapshot content" });

        var result = await retriever.RetrieveAsync(
            SessionId,
            "query",
            searchAllMyDocs: false,
            k: 5
        );

        Assert.Contains(result, b => b.Type == CacheBlockType.ProjectSnapshot);
    }

    private static ChatSession CreateSession(bool searchAllMyDocs, Guid? projectId = null)
    {
        return new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            SearchAllMyDocs = searchAllMyDocs,
            ProjectId = projectId,
        };
    }

    private static void SetupSessionFound(TestDeps deps, ChatSession session)
    {
        deps.SessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
    }
}
