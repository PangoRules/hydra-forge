namespace HydraForge.Application.Chat;

using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class LlmChatSummaryGenerator : IChatSummaryGenerator
{
    private readonly IChatSessionRepository _sessionRepo;
    private readonly IChatMessageRepository _messageRepo;
    private readonly IModelRouter _modelRouter;
    private readonly ILlmClientFactory _clientFactory;
    private readonly IUsageRecorder _usageRecorder;
    private readonly ILogger<LlmChatSummaryGenerator> _logger;

    public LlmChatSummaryGenerator(
        IChatSessionRepository sessionRepo,
        IChatMessageRepository messageRepo,
        IModelRouter modelRouter,
        ILlmClientFactory clientFactory,
        IUsageRecorder usageRecorder,
        ILogger<LlmChatSummaryGenerator> logger
    )
    {
        _sessionRepo = sessionRepo;
        _messageRepo = messageRepo;
        _modelRouter = modelRouter;
        _clientFactory = clientFactory;
        _usageRecorder = usageRecorder;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateSummaryAsync(
        Guid sessionId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<string>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        var messages = await _messageRepo.GetBySessionAsync(
            sessionId,
            before: null,
            beforeId: null,
            limit: 1000,
            ct
        );

        if (messages.Count == 0)
            return Result<string>.Success(string.Empty);

        var promptContent = BuildPrompt(messages);
        var estimatedTokens = TokenEstimator.EstimateTokens(promptContent);

        var routeResult = await _modelRouter.ResolveAsync(
            AiFeature.PersonalChat,
            session.OwnerId,
            projectId: null,
            estimatedTokens,
            ct
        );

        if (routeResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to resolve LLM route for session {SessionId}: {Error}",
                sessionId,
                routeResult.Error.Message
            );
            return Result<string>.Failure(
                new Error(DomainErrorCodes.Chat.SummaryFailed, "LLM routing failed.")
            );
        }

        var route = routeResult.Value;

        try
        {
            var chatRequest = new ChatRequest(
                route.Primary.Id,
                route.Primary.ModelId,
                [new ChatMessage(ChatRole.User, promptContent, null)],
                [],
                [],
                MaxOutputTokens: 500,
                Temperature: 0.3m,
                OllamaThinkMode: route.Primary.OllamaThinkMode
            );

            var llmClient = _clientFactory.For(route.Provider!);
            var summaryDeltas = new List<string>();
            var usageSnapshot = new UsageSnapshot(0, 0, 0);

            await foreach (var chunk in llmClient.StreamChatAsync(chatRequest, ct))
            {
                if (chunk.Delta != null)
                    summaryDeltas.Add(chunk.Delta);
                if (chunk.Usage != null)
                    usageSnapshot = chunk.Usage;

                if (chunk.FinishReason == ChatChunkFinishReason.Error)
                {
                    _logger.LogWarning(
                        "LLM returned error finish reason for session {SessionId}",
                        sessionId
                    );
                    return Result<string>.Failure(
                        new Error(DomainErrorCodes.Chat.SummaryFailed, "LLM returned an error.")
                    );
                }

                if (chunk.FinishReason == ChatChunkFinishReason.ContentFilter)
                {
                    _logger.LogWarning(
                        "LLM returned content filter for session {SessionId}",
                        sessionId
                    );
                    return Result<string>.Failure(
                        new Error(DomainErrorCodes.Chat.SummaryFailed, "Content filter triggered.")
                    );
                }
            }

            var summary = string.Join("", summaryDeltas);

            await _usageRecorder.RecordTokenAsync(
                new TokenUsageRecordInput(
                    session.OwnerId,
                    null,
                    AiFeature.PersonalChat,
                    route.Primary.Id,
                    route.PrimaryProvider.Id,
                    route.Primary.ModelId,
                    route.Primary.Name,
                    usageSnapshot.InputTokens,
                    usageSnapshot.OutputTokens,
                    usageSnapshot.CachedTokens,
                    null,
                    0m
                ),
                ct
            );

            return Result<string>.Success(summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM call failed for session {SessionId}", sessionId);
            return Result<string>.Failure(
                new Error(DomainErrorCodes.Chat.SummaryFailed, "LLM call failed.")
            );
        }
    }

    private static string BuildPrompt(IReadOnlyList<Domain.Entities.Chat.ChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Summarize this chat conversation in 2-3 sentences.");
        sb.AppendLine();
        foreach (var msg in messages)
        {
            var role = msg.Role == MessageRole.Assistant ? "Assistant" : "User";
            sb.AppendLine($"[{role}]: {msg.Content}");
        }
        return sb.ToString();
    }
}
