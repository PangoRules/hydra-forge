namespace HydraForge.Application.Chat;

using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class LlmChatTitleGenerator : IChatTitleGenerator
{
    private const int TitleMaxLength = 60;

    private readonly IModelRouter _modelRouter;
    private readonly ILlmClientFactory _clientFactory;
    private readonly IUsageRecorder _usageRecorder;
    private readonly ILogger<LlmChatTitleGenerator> _logger;

    public LlmChatTitleGenerator(
        IModelRouter modelRouter,
        ILlmClientFactory clientFactory,
        IUsageRecorder usageRecorder,
        ILogger<LlmChatTitleGenerator> logger
    )
    {
        _modelRouter = modelRouter;
        _clientFactory = clientFactory;
        _usageRecorder = usageRecorder;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateTitleAsync(
        Guid userId,
        string userMessage,
        string assistantMessage,
        CancellationToken ct = default
    )
    {
        var prompt = BuildPrompt(userMessage, assistantMessage);
        var estimatedTokens = TokenEstimator.EstimateTokens(prompt);

        var routeResult = await _modelRouter.ResolveAsync(
            AiFeature.ChatTitle,
            userId,
            projectId: null,
            estimatedTokens,
            ct
        );

        if (routeResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to resolve LLM route for chat title generation: {Error}",
                routeResult.Error.Message
            );
            return Result<string>.Failure(
                new Error(DomainErrorCodes.Chat.TitleFailed, "LLM routing failed.")
            );
        }

        var route = routeResult.Value;

        try
        {
            var chatRequest = new ChatRequest(
                route.Primary.Id,
                route.Primary.ModelId,
                [new ChatMessage(ChatRole.User, prompt, null)],
                [],
                [],
                MaxOutputTokens: 20,
                Temperature: 0.5m
            );

            var llmClient = _clientFactory.For(route.Provider!);
            var deltas = new List<string>();
            var usageSnapshot = new UsageSnapshot(0, 0, 0);

            await foreach (var chunk in llmClient.StreamChatAsync(chatRequest, ct))
            {
                if (chunk.Delta != null)
                    deltas.Add(chunk.Delta);
                if (chunk.Usage != null)
                    usageSnapshot = chunk.Usage;

                if (
                    chunk.FinishReason == ChatChunkFinishReason.Error
                    || chunk.FinishReason == ChatChunkFinishReason.ContentFilter
                )
                {
                    _logger.LogWarning("LLM returned an error/content-filter finish reason for chat title generation");
                    return Result<string>.Failure(
                        new Error(DomainErrorCodes.Chat.TitleFailed, "LLM returned an error.")
                    );
                }
            }

            var title = string.Join("", deltas).Trim().Trim('"');
            if (title.Length > TitleMaxLength)
            {
                title = title[..TitleMaxLength].TrimEnd() + "…";
            }

            await _usageRecorder.RecordTokenAsync(
                new TokenUsageRecordInput(
                    userId,
                    null,
                    AiFeature.ChatTitle,
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

            return string.IsNullOrWhiteSpace(title)
                ? Result<string>.Failure(
                    new Error(DomainErrorCodes.Chat.TitleFailed, "Empty title generated.")
                )
                : Result<string>.Success(title);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM call failed for chat title generation");
            return Result<string>.Failure(
                new Error(DomainErrorCodes.Chat.TitleFailed, "LLM call failed.")
            );
        }
    }

    private static string BuildPrompt(string userMessage, string assistantMessage) =>
        "Generate a short, concise title (3-6 words, no quotes, no trailing punctuation) "
        + "for a chat conversation, based on its opening exchange below. Reply with ONLY the title.\n\n"
        + $"[User]: {userMessage}\n[Assistant]: {assistantMessage}\n\nTitle:";
}
