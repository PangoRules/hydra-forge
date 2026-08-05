namespace HydraForge.Application.Chat;

using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class LlmChatTitleGenerator : IChatTitleGenerator
{
    private const int TitleMaxLength = 60;
    private const int MaxAttempts = 3;

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

        // Routing failure is a config problem (no model configured/enabled for the
        // feature), not a transient LLM hiccup — retrying won't change the outcome,
        // so resolve once and fail fast if that's broken.
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

        // The LLM call itself can fail transiently (empty response from a reasoning
        // model burning its budget on hidden thinking, a dropped stream, an echoed
        // non-title) — retry a few times before giving up and letting the caller's
        // crop-fallback take over as the last resort.
        Result<string> lastFailure = Result<string>.Failure(
            new Error(DomainErrorCodes.Chat.TitleFailed, "Title generation did not run.")
        );
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var result = await TryGenerateOnceAsync(userId, userMessage, prompt, route, ct);
            if (result.IsSuccess)
                return result;

            lastFailure = result;
            if (attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    "Chat title generation attempt {Attempt}/{MaxAttempts} failed for {ProviderName}/{ModelId}: {Error}",
                    attempt,
                    MaxAttempts,
                    route.Primary.Name,
                    route.Primary.ModelId,
                    result.Error.Message
                );
            }
        }

        _logger.LogWarning(
            "Chat title generation failed after {MaxAttempts} attempts for {ProviderName}/{ModelId}",
            MaxAttempts,
            route.Primary.Name,
            route.Primary.ModelId
        );
        return lastFailure;
    }

    private async Task<Result<string>> TryGenerateOnceAsync(
        Guid userId,
        string userMessage,
        string prompt,
        RouteDecision route,
        CancellationToken ct
    )
    {
        try
        {
            // Reasoning-capable models (confirmed live with DeepSeek V4 via OpenRouter)
            // spend the entire output budget on hidden reasoning tokens before ever
            // emitting visible title text, hitting the length cap with zero output.
            // Tried the OpenAI-style `reasoning_effort: "low"` field first — no effect;
            // OpenRouter's actual reasoning control is a nested `reasoning: {effort}`
            // object per-provider, which our adapter doesn't send, so this model likely
            // never saw the hint at all. Rather than chase that per-adapter, just budget
            // generously enough that reasoning tokens don't crowd out the real answer —
            // this is a once-per-session call, so the extra cost/latency is cheap. The
            // word-count guard below still rejects anything that isn't actually short.
            var chatRequest = new ChatRequest(
                route.Primary.Id,
                route.Primary.ModelId,
                [new ChatMessage(ChatRole.User, prompt, null)],
                [],
                [],
                MaxOutputTokens: 500,
                Temperature: 0.5m,
                OllamaThinkMode: route.Primary.OllamaThinkMode
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
                    return Result<string>.Failure(
                        new Error(DomainErrorCodes.Chat.TitleFailed, "LLM returned an error.")
                    );
                }
            }

            var rawTitle = string.Join("", deltas).Trim().Trim('"');

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

            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return Result<string>.Failure(
                    new Error(DomainErrorCodes.Chat.TitleFailed, "Empty title generated.")
                );
            }

            // Some models — observed with both cloud and local models — ignore the
            // "3-6 word title" instruction and echo the user's message back verbatim
            // instead. That's a 200 OK with real content, so IsSuccess alone can't
            // catch it. Two checks: exact echo (case-insensitive, punctuation-trimmed),
            // and word count wildly beyond what was asked for (a real title with a
            // couple extra words is fine; a full echoed sentence/paragraph is not —
            // word count catches this regardless of the user's own message length,
            // unlike a length-ratio check which would misfire on short messages).
            var normalizedTitle = rawTitle.Trim().TrimEnd('.', '!', '?');
            var normalizedUserMessage = userMessage.Trim().TrimEnd('.', '!', '?');
            var titleWordCount = normalizedTitle
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Length;
            if (
                normalizedTitle.Equals(normalizedUserMessage, StringComparison.OrdinalIgnoreCase)
                || titleWordCount > 12
            )
            {
                return Result<string>.Failure(
                    new Error(
                        DomainErrorCodes.Chat.TitleFailed,
                        "Generated title was an echo of the input."
                    )
                );
            }

            var title =
                normalizedTitle.Length > TitleMaxLength
                    ? normalizedTitle[..TitleMaxLength].TrimEnd() + "…"
                    : normalizedTitle;

            return Result<string>.Success(title);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        "Summarize the topic of the conversation below in a short title of 3-6 words. "
        + "Do NOT repeat or quote the user's message — write a new, condensed label for what "
        + "it's about. No quotes, no trailing punctuation. Reply with ONLY the title, nothing else.\n\n"
        + "Example:\n[User]: Can you help me refactor the authentication middleware to use JWT instead of sessions?\n"
        + "Title: JWT Authentication Refactor\n\n"
        + $"[User]: {userMessage}\n[Assistant]: {assistantMessage}\n\nTitle:";
}
