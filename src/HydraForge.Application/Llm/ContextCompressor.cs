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

        var userId = Guid.Empty;
        var routeResult = await _router.ResolveAsync(
            AiFeature.MemoryExtraction,
            userId,
            projectId: null,
            estimatedTokens,
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
        var client = _clientFactory.For(route.PrimaryProvider);
        var combinedContent = string.Join("\n\n", nonPinnedMemoryBlocks.Select(b => b.Content));
        var summaryPrompt =
            $"Summarize the following memory blocks into a concise narrative that preserves all key information:\n\n{combinedContent}";

        var chatRequest = new ChatRequest(
            route.Primary.Id,
            route.Primary.ModelId,
            [new ChatMessage(ChatRole.User, summaryPrompt)],
            [],
            [],
            MaxOutputTokens: null,
            Temperature: 0.3m
        );

        var summaryBuilder = new System.Text.StringBuilder();
        await foreach (var chunk in client.StreamChatAsync(chatRequest, ct))
        {
            if (chunk.Delta is not null)
            {
                summaryBuilder.Append(chunk.Delta);
            }
        }

        var summary = summaryBuilder.ToString();

        var newBlocks = blocks
            .Except(nonPinnedMemoryBlocks)
            .Append(new CacheBlock(summary, CacheBlockType.Memory, IsPinned: false))
            .ToList();

        var newEstimatedTokens = newBlocks.Sum(b => TokenEstimator.EstimateTokens(b.Content));

        return Result<CompressedContext>.Success(
            new CompressedContext(newBlocks, newEstimatedTokens, WasCompressed: true)
        );
    }
}
