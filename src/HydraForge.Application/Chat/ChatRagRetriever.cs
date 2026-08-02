namespace HydraForge.Application.Chat;

using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class ChatRagRetriever : IChatRagRetriever
{
    private readonly IModelRouter _modelRouter;
    private readonly ILlmClientFactory _clientFactory;
    private readonly IChatSessionRepository _sessionRepo;
    private readonly IChatSessionDocumentRepository _sessionDocRepo;
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly IProjectContextSnapshotRepository _snapshotRepo;
    private readonly ILogger<ChatRagRetriever> _logger;

    public ChatRagRetriever(
        IModelRouter modelRouter,
        ILlmClientFactory clientFactory,
        IChatSessionRepository sessionRepo,
        IChatSessionDocumentRepository sessionDocRepo,
        IDocumentChunkRepository chunkRepo,
        IProjectContextSnapshotRepository snapshotRepo,
        ILogger<ChatRagRetriever> logger
    )
    {
        _modelRouter = modelRouter;
        _clientFactory = clientFactory;
        _sessionRepo = sessionRepo;
        _sessionDocRepo = sessionDocRepo;
        _chunkRepo = chunkRepo;
        _snapshotRepo = snapshotRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CacheBlock>> RetrieveAsync(
        Guid sessionId,
        string query,
        bool searchAllMyDocs,
        int k,
        CancellationToken ct = default
    )
    {
        var blocks = new List<CacheBlock>();

        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return blocks;

        var embedResult = await EmbedQueryAsync(query, session.OwnerId, ct);
        if (embedResult.IsFailure)
        {
            _logger.LogWarning("RAG embedding failed for session {SessionId}: {Error}", sessionId, embedResult.Error.Message);
            return blocks;
        }

        var queryEmbedding = embedResult.Value.Vectors[0];

        IReadOnlyList<Guid>? sessionDocIds = null;
        if (!searchAllMyDocs)
        {
            var sessionDocs = await _sessionDocRepo.GetBySessionAsync(sessionId, ct);
            if (sessionDocs.Count == 0)
                return blocks;
            sessionDocIds = sessionDocs.Select(d => d.DocumentId).ToList();
        }

        var chunks = await _chunkRepo.SearchAsync(session.OwnerId, sessionDocIds, queryEmbedding, k, ct);
        if (chunks.Count == 0)
            return blocks;

        var concatenatedContent = string.Join("\n\n", chunks.Select(c => c.Content));
        blocks.Add(new CacheBlock(concatenatedContent, CacheBlockType.SystemContext));

        if (session.ProjectId != null)
        {
            var snapshot = await _snapshotRepo.GetByProjectIdAsync(session.ProjectId.Value, ct);
            if (snapshot != null)
                blocks.Insert(0, new CacheBlock(snapshot.TemplateContent, CacheBlockType.ProjectSnapshot));
        }

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
