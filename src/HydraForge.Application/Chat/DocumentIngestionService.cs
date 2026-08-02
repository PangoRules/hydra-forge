namespace HydraForge.Application.Chat;

using HydraForge.Application.Attachments;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;

public sealed class DocumentIngestionService
{
    private readonly IDocumentRepository _documentRepo;
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly IFileStore _fileStore;
    private readonly IModelRouter _modelRouter;
    private readonly ILlmClientFactory _clientFactory;

    public DocumentIngestionService(
        IDocumentRepository documentRepo,
        IDocumentChunkRepository chunkRepo,
        IFileStore fileStore,
        IModelRouter modelRouter,
        ILlmClientFactory clientFactory
    )
    {
        _documentRepo = documentRepo;
        _chunkRepo = chunkRepo;
        _fileStore = fileStore;
        _modelRouter = modelRouter;
        _clientFactory = clientFactory;
    }

    private const int ChunkSizeChars = 2000;
    private const int OverlapChars = 400;

    public async Task<Result<Document>> IngestAsync(
        Guid userId,
        string title,
        string content,
        string contentType,
        Stream? stream = null,
        CancellationToken ct = default
    )
    {
        if (stream is not null)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            content = content ?? string.Empty;
            content += ms.Length > 0
                ? System.Text.Encoding.UTF8.GetString(ms.ToArray())
                : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<Document>.Failure(
                new Error(DomainErrorCodes.Chat.EmbeddingFailed, "Content is empty.")
            );
        }

        var chunks = ChunkText(content);

        var estimatedTokens = chunks.Sum(c => TokenEstimator.EstimateTokens(c));
        var routeResult = await _modelRouter.ResolveAsync(
            AiFeature.PersonalChat,
            userId,
            projectId: null,
            estimatedTokens,
            ct
        );

        if (routeResult.IsFailure)
        {
            return Result<Document>.Failure(
                new Error(DomainErrorCodes.Chat.EmbeddingFailed, routeResult.Error.Message)
            );
        }

        var route = routeResult.Value;
        var embeddingClient = _clientFactory.EmbeddingFor(route.Provider!);

        var embedResult = await embeddingClient.EmbedAsync(
            new EmbeddingRequest(route.Primary.Id, route.Primary.ModelId, chunks),
            ct
        );

        if (embedResult.IsFailure)
        {
            return Result<Document>.Failure(
                new Error(DomainErrorCodes.Chat.EmbeddingFailed, embedResult.Error.Message)
            );
        }

        var vectors = embedResult.Value.Vectors;

        var documentId = Guid.NewGuid();
        string? storagePath = null;

        if (stream is not null)
        {
            stream.Position = 0;
            var key = $"{userId}/document/{documentId}/{Guid.NewGuid()}";
            var storeResult = await _fileStore.StoreAsync(stream, contentType, key, ct);
            if (storeResult.IsFailure)
            {
                return Result<Document>.Failure(storeResult.Error);
            }
            storagePath = storeResult.Value;
        }

        var now = DateTime.UtcNow;
        var document = new Document
        {
            Id = documentId,
            UserId = userId,
            Title = title,
            Content = content,
            ContentType = contentType,
            FilePath = storagePath,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _documentRepo.AddAsync(document, ct);

        var docChunks = new List<DocumentChunk>(vectors.Count);
        for (var i = 0; i < vectors.Count; i++)
        {
            docChunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DocumentId = documentId,
                SourceType = "document",
                SourceId = documentId,
                ChunkIndex = i,
                Content = chunks[i],
                Embedding = new Pgvector.Vector(vectors[i].Span.ToArray()),
                CreatedAt = now,
            });
        }

        await _chunkRepo.AddRangeAsync(docChunks, ct);

        return Result<Document>.Success(document);
    }

    private static IReadOnlyList<string> ChunkText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + ChunkSizeChars, text.Length);
            if (end < text.Length)
            {
                var overlapStart = Math.Max(0, end - OverlapChars);
                var window = text.AsSpan(overlapStart, end - overlapStart);
                var lastNewline = window.LastIndexOf('\n');
                if (lastNewline > ChunkSizeChars / 4)
                {
                    end = overlapStart + lastNewline + 1;
                }
            }
            chunks.Add(text[start..end]);
            start = end;
        }

        return chunks;
    }
}
