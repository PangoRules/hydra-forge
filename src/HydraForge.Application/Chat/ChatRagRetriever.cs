namespace HydraForge.Application.Chat;

using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class ChatRagRetriever : IChatRagRetriever
{
    private readonly IModelRouter _modelRouter;
    private readonly ILlmClientFactory _clientFactory;
    private readonly IChatSessionRepository _sessionRepo;
    private readonly IChatMessageRepository _messageRepo;
    private readonly IChatSessionDocumentRepository _sessionDocRepo;
    private readonly IAgentPersonalityRepository _personalityRepo;
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly IProjectContextSnapshotRepository _snapshotRepo;
    private readonly ILogger<ChatRagRetriever> _logger;

    public ChatRagRetriever(
        IModelRouter modelRouter,
        ILlmClientFactory clientFactory,
        IChatSessionRepository sessionRepo,
        IChatMessageRepository messageRepo,
        IChatSessionDocumentRepository sessionDocRepo,
        IAgentPersonalityRepository personalityRepo,
        IDocumentChunkRepository chunkRepo,
        IProjectContextSnapshotRepository snapshotRepo,
        ILogger<ChatRagRetriever> logger
    )
    {
        _modelRouter = modelRouter;
        _clientFactory = clientFactory;
        _sessionRepo = sessionRepo;
        _messageRepo = messageRepo;
        _sessionDocRepo = sessionDocRepo;
        _personalityRepo = personalityRepo;
        _chunkRepo = chunkRepo;
        _snapshotRepo = snapshotRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CacheBlock>> RetrieveAsync(
        Guid sessionId,
        string query,
        bool searchAllMyDocs,
        string? presetContent,
        int k,
        CancellationToken ct = default
    )
    {
        var blocks = new List<CacheBlock>();

        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return blocks;

        // Embed user message
        var embedResult = await EmbedQueryAsync(query, session.OwnerId, ct);
        if (embedResult.IsFailure)
        {
            _logger.LogWarning("RAG embedding failed for session {SessionId}: {Error}", sessionId, embedResult.Error.Message);
            return blocks;
        }

        var queryEmbedding = embedResult.Value.Vectors[0];

        // Build candidate document IDs
        IReadOnlyList<Guid>? sessionDocIds = null;
        if (!searchAllMyDocs)
        {
            var sessionDocs = await _sessionDocRepo.GetBySessionAsync(sessionId, ct);
            if (sessionDocs.Count == 0)
                return blocks;
            sessionDocIds = sessionDocs.Select(d => d.DocumentId).ToList();
        }

        // pgvector similarity search
        var chunks = await _chunkRepo.SearchAsync(session.OwnerId, sessionDocIds, queryEmbedding, k, ct);
        if (chunks.Count == 0)
            return blocks;

        // RAG CacheBlock — concatenated chunk texts, no cache_control
        var concatenatedContent = string.Join("\n\n", chunks.Select(c => c.Content));
        blocks.Add(new CacheBlock(concatenatedContent, CacheBlockType.SystemContext));

        // Project snapshot on session's first message
        if (session.ProjectId != null)
        {
            var priorMessages = await _messageRepo.GetBySessionAsync(sessionId, null, 1, ct);
            if (priorMessages.Count == 0)
            {
                var snapshot = await _snapshotRepo.GetByProjectIdAsync(session.ProjectId.Value, ct);
                if (snapshot != null)
                    blocks.Insert(0, new CacheBlock(snapshot.TemplateContent, CacheBlockType.ProjectSnapshot));
            }
        }

        // Personality system prompt
        if (session.PersonalityId != null)
        {
            var personality = await _personalityRepo.GetByIdAsync(session.PersonalityId.Value, ct);
            if (personality != null && personality.ArchivedAt == null)
            {
                blocks.Insert(0, new CacheBlock(personality.SystemPrompt, CacheBlockType.SystemContext));
            }
        }

        // Prompt preset injection — prepend to user message wrapped in <preset> tags
        if (!string.IsNullOrWhiteSpace(presetContent))
        {
            var wrappedPreset = $"<preset>\n{presetContent}\n</preset>";
            blocks.Add(new CacheBlock(wrappedPreset, CacheBlockType.SystemContext));
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
