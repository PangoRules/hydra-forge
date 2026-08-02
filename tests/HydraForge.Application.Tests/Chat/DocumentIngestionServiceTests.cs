namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Attachments;
using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;
using NSubstitute;

public class DocumentIngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_EmptyContent_ReturnsFailure()
    {
        var (service, _, _, _, _) = CreateSut();

        var result = await service.IngestAsync(
            Guid.NewGuid(),
            "Test Doc",
            string.Empty,
            "text/plain"
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.EmbeddingFailed, result.Error.Code);
    }

    [Fact]
    public async Task IngestAsync_WhitespaceOnlyContent_ReturnsFailure()
    {
        var (service, _, _, _, _) = CreateSut();

        var result = await service.IngestAsync(
            Guid.NewGuid(),
            "Test Doc",
            "   ",
            "text/plain"
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.EmbeddingFailed, result.Error.Code);
    }

    [Fact]
    public async Task IngestAsync_ShortContent_ProducesOneChunk()
    {
        var userId = Guid.NewGuid();
        var content = new string('x', 500);
        var (service, _, mockChunkRepo, _, mockEmbeddingClient) = CreateSut();
        SetupSuccessfulEmbedding(mockEmbeddingClient, 1);

        var result = await service.IngestAsync(userId, "Test", content, "text/plain");

        Assert.True(result.IsSuccess);
        var doc = result.Value;
        Assert.Equal("Test", doc.Title);
        Assert.Equal(userId, doc.UserId);
        Assert.Equal(1, mockChunkRepo.CapturedChunks.Count);
    }

    [Fact]
    public async Task IngestAsync_MultiChunk_RespectsChunkBoundaries()
    {
        // 4000 chars: chunk[0]=0..2000, chunk[1]=2000..4000 → 2 chunks
        var content = new string('x', 4000);
        var (service, _, mockChunkRepo, _, mockEmbeddingClient) = CreateSut();
        SetupSuccessfulEmbedding(mockEmbeddingClient, 2);

        var result = await service.IngestAsync(Guid.NewGuid(), "Test", content, "text/plain");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, mockChunkRepo.CapturedChunks.Count);
        Assert.Equal(0, mockChunkRepo.CapturedChunks[0].ChunkIndex);
        Assert.Equal(1, mockChunkRepo.CapturedChunks[1].ChunkIndex);
    }

    [Fact]
    public async Task IngestAsync_EmbeddingFails_ReturnsFailureAndDoesNotPersist()
    {
        var (service, mockDocRepo, mockChunkRepo, _, mockEmbeddingClient) = CreateSut();
        SetupFailedEmbedding(mockEmbeddingClient);

        var result = await service.IngestAsync(Guid.NewGuid(), "Test", "some content", "text/plain");

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.EmbeddingFailed, result.Error.Code);
        Assert.Empty(mockDocRepo.CapturedDocuments);
        Assert.Empty(mockChunkRepo.CapturedChunks);
    }

    [Fact]
    public async Task IngestAsync_WithStream_StoresFileAndSetsFilePath()
    {
        var userId = Guid.NewGuid();
        var stream = new MemoryStream([1, 2, 3]);
        var (service, _, mockChunkRepo, _, mockEmbeddingClient) = CreateSut();
        SetupSuccessfulEmbedding(mockEmbeddingClient, 1);

        var result = await service.IngestAsync(userId, "Test", "", "application/octet-stream", stream);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.FilePath);
        Assert.StartsWith($"{userId}/document/", result.Value.FilePath);
        Assert.Equal(1, mockChunkRepo.CapturedChunks.Count);
    }

    [Fact]
    public async Task IngestAsync_WithStream_NoDocumentChunkRowsLeftOrphanedOnEmbeddingFailure()
    {
        var (service, mockDocRepo, mockChunkRepo, _, mockEmbeddingClient) = CreateSut();
        SetupFailedEmbedding(mockEmbeddingClient);
        var stream = new MemoryStream([1, 2, 3]);

        await service.IngestAsync(Guid.NewGuid(), "Test", "", "application/octet-stream", stream);

        Assert.Empty(mockDocRepo.CapturedDocuments);
        Assert.Empty(mockChunkRepo.CapturedChunks);
    }

    private static (DocumentIngestionService, InMemoryDocumentRepository, InMemoryChunkRepository, InMemoryFileStore, IEmbeddingClient) CreateSut()
    {
        var docRepo = new InMemoryDocumentRepository();
        var chunkRepo = new InMemoryChunkRepository();
        var fileStore = new InMemoryFileStore();
        var modelRouter = Substitute.For<IModelRouter>();
        var clientFactory = Substitute.For<ILlmClientFactory>();
        var embeddingClient = Substitute.For<IEmbeddingClient>();

        var provider = new HydraForge.Domain.Entities.Admin.LlmProvider
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
        var routeResult = Result<RouteDecision>.Success(routeDecision);

        modelRouter
            .ResolveAsync(
                AiFeature.PersonalChat,
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(routeResult);

        clientFactory.EmbeddingFor(provider).Returns(embeddingClient);

        var service = new DocumentIngestionService(docRepo, chunkRepo, fileStore, modelRouter, clientFactory);
        return (service, docRepo, chunkRepo, fileStore, embeddingClient);
    }

    private static void SetupSuccessfulEmbedding(IEmbeddingClient mock, int chunkCount)
    {
        var vectors = Enumerable.Range(0, chunkCount)
            .Select(_ => (ReadOnlyMemory<float>)new float[1536])
            .ToList();
        mock.EmbedAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingResult>.Success(new EmbeddingResult(vectors)));
    }

    private static void SetupFailedEmbedding(IEmbeddingClient mock)
    {
        mock.EmbedAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingResult>.Failure(
                new Error("EMBEDDING_FAILED", "Embedding request failed")));
    }

    private sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        public List<Document> CapturedDocuments { get; } = [];

        public Task AddAsync(Document document, CancellationToken ct = default)
        {
            CapturedDocuments.Add(document);
            return Task.CompletedTask;
        }

        public Task ArchiveAsync(Guid documentId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<Document?> GetByIdAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult<Document?>(null);

        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>([]);
    }

    private sealed class InMemoryChunkRepository : IDocumentChunkRepository
    {
        public List<DocumentChunk> CapturedChunks { get; } = [];

        public Task AddAsync(DocumentChunk chunk, CancellationToken ct = default)
        {
            CapturedChunks.Add(chunk);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default)
        {
            CapturedChunks.AddRange(chunks);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryFileStore : IFileStore
    {
        public Task<Result<string>> StoreAsync(
            Stream content,
            string contentType,
            string storageKey,
            CancellationToken ct = default
        ) => Task.FromResult(Result<string>.Success(storageKey));

        public Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken ct = default) =>
            Task.FromResult(Result<Stream>.Success(new MemoryStream()));

        public Task<Result> DeleteAsync(string storageKey, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());
    }
}
