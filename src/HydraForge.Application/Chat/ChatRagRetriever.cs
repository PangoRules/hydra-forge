namespace HydraForge.Application.Chat;

using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ChatRagRetriever : IChatRagRetriever
{
    private readonly IModelRouter _modelRouter;
    private readonly ILlmClientFactory _clientFactory;
    private readonly IChatSessionRepository _sessionRepo;
    private readonly IChatSessionDocumentRepository _sessionDocRepo;
    private readonly IChatMessageRepository _messageRepo;
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly IProjectContextSnapshotRepository _snapshotRepo;
    private readonly IOptions<RagOptions> _ragOptions;
    private readonly ILogger<ChatRagRetriever> _logger;

    public ChatRagRetriever(
        IModelRouter modelRouter,
        ILlmClientFactory clientFactory,
        IChatSessionRepository sessionRepo,
        IChatSessionDocumentRepository sessionDocRepo,
        IChatMessageRepository messageRepo,
        IDocumentChunkRepository chunkRepo,
        IProjectContextSnapshotRepository snapshotRepo,
        IOptions<RagOptions> ragOptions,
        ILogger<ChatRagRetriever> logger
    )
    {
        _modelRouter = modelRouter;
        _clientFactory = clientFactory;
        _sessionRepo = sessionRepo;
        _sessionDocRepo = sessionDocRepo;
        _messageRepo = messageRepo;
        _chunkRepo = chunkRepo;
        _snapshotRepo = snapshotRepo;
        _ragOptions = ragOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CacheBlock>> RetrieveAsync(
        Guid sessionId,
        string query,
        bool searchAllMyDocs,
        int? k,
        CancellationToken ct = default
    )
    {
        var blocks = new List<CacheBlock>();

        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return blocks;

        var priorMessages = await _messageRepo.GetBySessionAsync(
            sessionId,
            before: null,
            beforeId: null,
            limit: 1,
            ct
        );
        bool isFirstMessage = priorMessages.Count(m => m.Role != MessageRole.System) == 0;

        CacheBlock? snapshotBlock = null;
        if (session.ProjectId != null && isFirstMessage)
        {
            var snapshot = await _snapshotRepo.GetByProjectIdAsync(session.ProjectId.Value, ct);
            if (snapshot != null)
                snapshotBlock = new CacheBlock(
                    snapshot.TemplateContent,
                    CacheBlockType.ProjectSnapshot
                );
        }

        var embedResult = await EmbedQueryAsync(query, session.OwnerId, ct);
        if (embedResult.IsFailure)
        {
            _logger.LogWarning(
                "RAG embedding failed for session {SessionId}: {Error}",
                sessionId,
                embedResult.Error.Message
            );
            if (snapshotBlock != null)
                blocks.Add(snapshotBlock);
            return blocks;
        }

        if (embedResult.Value.Vectors.Count == 0)
        {
            _logger.LogWarning(
                "RAG embedding returned empty vectors for session {SessionId}",
                sessionId
            );
            if (snapshotBlock != null)
                blocks.Add(snapshotBlock);
            return blocks;
        }

        var queryEmbedding = embedResult.Value.Vectors[0];

        IReadOnlyList<Guid>? sessionDocIds = null;
        if (!searchAllMyDocs)
        {
            var sessionDocs = await _sessionDocRepo.GetBySessionAsync(sessionId, ct);
            if (sessionDocs.Count == 0)
            {
                if (snapshotBlock != null)
                    blocks.Add(snapshotBlock);
                return blocks;
            }
            sessionDocIds = sessionDocs.Select(d => d.DocumentId).ToList();
        }

        var effectiveK = k ?? _ragOptions.Value.TopK;
        var chunks = await _chunkRepo.SearchAsync(
            session.OwnerId,
            sessionDocIds,
            queryEmbedding,
            effectiveK,
            ct
        );
        if (chunks.Count == 0)
        {
            if (snapshotBlock != null)
                blocks.Add(snapshotBlock);
            return blocks;
        }

        var concatenatedContent = string.Join("\n\n", chunks.Select(c => c.Content));
        blocks.Add(new CacheBlock(concatenatedContent, CacheBlockType.RagContext));

        if (snapshotBlock != null)
            blocks.Insert(0, snapshotBlock);

        return blocks;
    }

    private async Task<Result<EmbeddingResult>> EmbedQueryAsync(
        string query,
        Guid userId,
        CancellationToken ct
    )
    {
        var routeResult = await _modelRouter.ResolveAsync(
            AiFeature.DocumentEmbedding,
            userId,
            projectId: null,
            estimatedTokens: TokenEstimator.EstimateTokens(query),
            ct
        );

        if (routeResult.IsFailure)
            return Result<EmbeddingResult>.Failure(routeResult.Error);

        var route = routeResult.Value;
        var embeddingClient = _clientFactory.EmbeddingFor(route.Provider!);

        return await embeddingClient.EmbedAsync(
            new EmbeddingRequest(route.Primary.Id, route.Primary.ModelId, [query]),
            ct
        );
    }
}
