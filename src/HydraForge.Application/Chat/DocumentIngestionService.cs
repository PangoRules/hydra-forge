namespace HydraForge.Application.Chat;

using HydraForge.Application.Attachments;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;

public sealed class DocumentIngestionService : IDocumentIngestionService
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

    // Conservative cap on inputs per EmbedAsync call — ProviderModelConfig has no
    // "max batch input" field to read the real limit from, so a fixed safe constant
    // keeps large documents (up to the 10 MB file-store max) from blowing past
    // provider-side batch limits (e.g. OpenAI's 2048-input cap) in a single request.
    private const int EmbedBatchSize = 100;

    public async Task<Result<Document>> IngestAsync(
        Guid userId,
        string title,
        string content,
        string contentType,
        Stream? stream = null,
        CancellationToken ct = default
    )
    {
        Stream? seekableStream = null;
        try
        {
            if (stream is not null)
            {
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                ms.Position = 0;
                seekableStream = ms;

                using var reader = new StreamReader(ms, System.Text.Encoding.UTF8, leaveOpen: true);
                var fromStream = await reader.ReadToEndAsync();
                ms.Position = 0;
                content = (content ?? string.Empty) + fromStream;
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
                AiFeature.DocumentEmbedding,
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

            var vectors = new List<ReadOnlyMemory<float>>(chunks.Count);
            for (var batchStart = 0; batchStart < chunks.Count; batchStart += EmbedBatchSize)
            {
                var batch = chunks.Skip(batchStart).Take(EmbedBatchSize).ToList();
                var embedResult = await embeddingClient.EmbedAsync(
                    new EmbeddingRequest(route.Primary.Id, route.Primary.ModelId, batch),
                    ct
                );

                if (embedResult.IsFailure)
                {
                    return Result<Document>.Failure(
                        new Error(DomainErrorCodes.Chat.EmbeddingFailed, embedResult.Error.Message)
                    );
                }

                vectors.AddRange(embedResult.Value.Vectors);
            }

            if (vectors.Count != chunks.Count)
            {
                return Result<Document>.Failure(
                    new Error(
                        DomainErrorCodes.Chat.EmbeddingFailed,
                        $"Embedding returned {vectors.Count} vectors for {chunks.Count} chunks."
                    )
                );
            }

            var documentId = Guid.NewGuid();
            string? storagePath = null;

            if (seekableStream is not null)
            {
                seekableStream.Position = 0;
                var key = $"{userId}/document/{documentId}/{Guid.NewGuid()}";
                var storeResult = await _fileStore.StoreAsync(seekableStream, contentType, key, ct);
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
                docChunks.Add(
                    new DocumentChunk
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
                    }
                );
            }

            await _chunkRepo.AddRangeAsync(docChunks, ct);

            return Result<Document>.Success(document);
        }
        finally
        {
            seekableStream?.Dispose();
        }
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

                // Threshold is relative to the overlap window (its actual size), not the
                // full chunk size — the window itself is only OverlapChars long, so a
                // ChunkSizeChars-relative threshold could never be satisfied and this
                // boundary snap silently never fired.
                if (lastNewline > OverlapChars / 4)
                {
                    end = overlapStart + lastNewline + 1;
                }
            }
            chunks.Add(text[start..end]);
            start = end < text.Length ? end - OverlapChars : text.Length;
        }

        return chunks;
    }
}
