namespace HydraForge.Application.Llm;

using HydraForge.Application.Logging;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ContextCompressor : IContextCompressor
{
    private readonly IModelRouter _router;
    private readonly ILlmClientFactory _clientFactory;
    private readonly ILogger<ContextCompressor> _logger;
    private readonly double _thresholdRatio;

    public ContextCompressor(
        IModelRouter router,
        ILlmClientFactory clientFactory,
        ILogger<ContextCompressor> logger,
        IOptions<LlmOptions> options
    )
    {
        _router = router;
        _clientFactory = clientFactory;
        _logger = logger;
        _thresholdRatio = options.Value.ContextCompressionThresholdRatio;
    }

    public async Task<Result<CompressedContext>> CompressAsync(
        IReadOnlyList<CacheBlock> blocks,
        int modelMaxTokens,
        CancellationToken ct = default
    )
    {
        if (blocks.Count == 0)
        {
            return Result<CompressedContext>.Success(
                new CompressedContext(blocks, 0, WasCompressed: false)
            );
        }

        var estimatedTokens = blocks.Sum(b => TokenEstimator.EstimateTokens(b.Content));
        var threshold = (int)(modelMaxTokens * _thresholdRatio);

        if (estimatedTokens <= threshold)
        {
            return Result<CompressedContext>.Success(
                new CompressedContext(blocks, estimatedTokens, WasCompressed: false)
            );
        }

        var nonPinnedMemoryBlocks = blocks
            .Where(b => b.Type == CacheBlockType.Memory && !b.IsPinned)
            .ToList();

        if (nonPinnedMemoryBlocks.Count == 0)
        {
            return Result<CompressedContext>.Success(
                new CompressedContext(blocks, estimatedTokens, WasCompressed: false)
            );
        }

        // Take oldest non-pinned blocks (first in list = oldest) until the remaining
        // blocks would be under threshold if the accumulated ones were replaced by one summary.
        // Track by index in the original blocks list to avoid record equality issues.
        var accumulated = new List<CacheBlock>();
        var accumulatedIndices = new HashSet<int>();
        var blocksWithIndex = blocks.Select((b, i) => (block: b, index: i)).ToList();
        var nonPinnedWithIndex = blocksWithIndex
            .Where(x => x.block.Type == CacheBlockType.Memory && !x.block.IsPinned)
            .ToList();

        foreach (var (block, index) in nonPinnedWithIndex)
        {
            accumulated.Add(block);
            accumulatedIndices.Add(index);
            var remainingBlocks = blocksWithIndex
                .Where(x => !accumulatedIndices.Contains(x.index))
                .Select(x => x.block)
                .ToList();
            var remainingTokens = remainingBlocks.Sum(b =>
                TokenEstimator.EstimateTokens(b.Content)
            );
            var summaryTokens = accumulated.Sum(b => TokenEstimator.EstimateTokens(b.Content)) / 2;
            if (remainingTokens + summaryTokens <= threshold)
            {
                break;
            }
        }

        var combinedContent = string.Join("\n\n", accumulated.Select(b => b.Content));
        var summaryPromptTokens = TokenEstimator.EstimateTokens(combinedContent);

        var userId = Guid.Empty;
        var routeResult = await _router.ResolveAsync(
            AiFeature.MemoryExtraction,
            userId,
            projectId: null,
            summaryPromptTokens,
            ct
        );

        if (routeResult.IsFailure)
        {
            _logger.LogWarning(
                "ContextCompressor: no Economy model available for MemoryExtraction. Returning uncompressed context. Error: {Error}",
                routeResult.Error.Message
            );
            return Result<CompressedContext>.Success(
                new CompressedContext(blocks, estimatedTokens, WasCompressed: false)
            );
        }

        var route = routeResult.Value;
        var client = _clientFactory.For(route.Provider!);
        var summaryPrompt =
            $"Summarize the following memory blocks into a concise narrative that preserves all key information:\n\n{combinedContent}";

        var chatRequest = new ChatRequest(
            route.Primary.Id,
            route.Primary.ModelId,
            [new ChatMessage(ChatRole.User, summaryPrompt)],
            [],
            [],
            MaxOutputTokens: null,
            Temperature: 0.3m,
            OllamaThinkMode: route.Primary.OllamaThinkMode
        );

        string summary;
        try
        {
            var summaryBuilder = new System.Text.StringBuilder();
            await foreach (var chunk in client.StreamChatAsync(chatRequest, ct))
            {
                if (chunk.Delta is not null)
                {
                    summaryBuilder.Append(chunk.Delta);
                }
                if (
                    chunk.FinishReason
                    is ChatChunkFinishReason.Error
                        or ChatChunkFinishReason.ContentFilter
                )
                {
                    _logger.LogWarning(
                        "ContextCompressor: summarization received {FinishReason} chunk. Returning uncompressed context.",
                        chunk.FinishReason
                    );
                    return Result<CompressedContext>.Success(
                        new CompressedContext(blocks, estimatedTokens, WasCompressed: false)
                    );
                }
            }
            summary = summaryBuilder.ToString();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "ContextCompressor: summarization failed ({Provider}/{Model}). Returning uncompressed context. Error: {Error}",
                route.PrimaryProvider,
                route.Primary.ModelId,
                ex.Message
            );
            return Result<CompressedContext>.Success(
                new CompressedContext(blocks, estimatedTokens, WasCompressed: false)
            );
        }

        var newBlocks = blocks.Select((b, i) => accumulatedIndices.Contains(i) ? null : b).ToList();
        var firstAccumulatedIndex = accumulatedIndices.Min();
        newBlocks[firstAccumulatedIndex] = new CacheBlock(
            summary,
            CacheBlockType.Memory,
            IsPinned: false
        );
        var cleanedBlocks = newBlocks.Where(b => b is not null).Select(b => b!).ToList();

        var newEstimatedTokens = cleanedBlocks.Sum(b => TokenEstimator.EstimateTokens(b.Content));

        return Result<CompressedContext>.Success(
            new CompressedContext(cleanedBlocks, newEstimatedTokens, WasCompressed: true)
        );
    }
}
