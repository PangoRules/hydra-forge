namespace HydraForge.Application.Tests.Llm;

using HydraForge.Application.Llm;
using HydraForge.Application.Logging;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

public class ContextCompressorTests
{
    private static LlmOptions DefaultOptions() => new() { ContextCompressionThresholdRatio = 0.75 };

    private static ContextCompressor CreateCompressor(
        IModelRouter router,
        ILlmClientFactory? clientFactory = null,
        LlmOptions? options = null
    )
    {
        var logger = new Mock<ILogger<ContextCompressor>>();
        var opts = Options.Create(options ?? DefaultOptions());
        var factory = clientFactory ?? new Mock<ILlmClientFactory>().Object;
        return new ContextCompressor(router, factory, logger.Object, opts);
    }

    [Fact]
    public async Task CompressAsync_UnderThreshold_ReturnsUncompressed()
    {
        var router = new Mock<IModelRouter>();
        var blocks = new List<CacheBlock> { new("short memory content", CacheBlockType.Memory) };
        var compressor = CreateCompressor(router.Object);

        var result = await compressor.CompressAsync(blocks, modelMaxTokens: 1000);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.WasCompressed);
        Assert.Equal(blocks, result.Value.Blocks);
    }

    [Fact]
    public async Task CompressAsync_OverThreshold_TriggersCompression()
    {
        var mockClient = new Mock<ILlmClient>();
        var summaryChunks = new List<ChatChunk>
        {
            new("Summarized ", null, null),
            new("content ", null, null),
            new("here.", null, null),
        };
        mockClient
            .Setup(c => c.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableChunkList(summaryChunks));

        var mockFactory = new Mock<ILlmClientFactory>();
        mockFactory.Setup(f => f.For(It.IsAny<LlmProvider>())).Returns(mockClient.Object);

        var mockRouter = new Mock<IModelRouter>();
        var routeDecision = new RouteDecision(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "economy-model",
                "economy-model-name",
                "Economy",
                0.001m,
                4096,
                true
            ),
            new ProviderDto(
                Guid.NewGuid(),
                "TestProvider",
                "https://test.com",
                "OpenAiCompatible",
                "Text",
                "Economy",
                null,
                true,
                DateTime.UtcNow,
                DateTime.UtcNow
            ),
            [],
            Provider: null
        );
        mockRouter
            .Setup(r =>
                r.ResolveAsync(
                    AiFeature.MemoryExtraction,
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RouteDecision>.Success(routeDecision));

        var blocks = new List<CacheBlock>
        {
            new(new string('x', 500), CacheBlockType.Memory),
            new(new string('y', 500), CacheBlockType.Memory),
        };
        var compressor = CreateCompressor(mockRouter.Object, mockFactory.Object);

        var result = await compressor.CompressAsync(blocks, modelMaxTokens: 100);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.WasCompressed);
        Assert.Single(result.Value.Blocks);
        Assert.Equal(CacheBlockType.Memory, result.Value.Blocks[0].Type);
        Assert.Equal("Summarized content here.", result.Value.Blocks[0].Content);
    }

    [Fact]
    public async Task CompressAsync_PinnedBlocks_Preserved()
    {
        var mockClient = new Mock<ILlmClient>();
        var summaryChunks = new List<ChatChunk>
        {
            new("Summarized pinned test content.", null, null),
        };
        mockClient
            .Setup(c => c.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableChunkList(summaryChunks));

        var mockFactory = new Mock<ILlmClientFactory>();
        mockFactory.Setup(f => f.For(It.IsAny<LlmProvider>())).Returns(mockClient.Object);

        var mockRouter = new Mock<IModelRouter>();
        var routeDecision = new RouteDecision(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "economy-model",
                "economy-model-name",
                "Economy",
                0.001m,
                4096,
                true
            ),
            new ProviderDto(
                Guid.NewGuid(),
                "TestProvider",
                "https://test.com",
                "OpenAiCompatible",
                "Text",
                "Economy",
                null,
                true,
                DateTime.UtcNow,
                DateTime.UtcNow
            ),
            [],
            Provider: null
        );
        mockRouter
            .Setup(r =>
                r.ResolveAsync(
                    AiFeature.MemoryExtraction,
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RouteDecision>.Success(routeDecision));

        // Pinned block + two large non-pinned blocks that exceed threshold.
        var blocks = new List<CacheBlock>
        {
            new("pinned memory", CacheBlockType.Memory, IsPinned: true),
            new(new string('a', 500), CacheBlockType.Memory),
            new(new string('b', 500), CacheBlockType.Memory),
        };
        var compressor = CreateCompressor(mockRouter.Object, mockFactory.Object);

        var result = await compressor.CompressAsync(blocks, modelMaxTokens: 100);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.WasCompressed);
        Assert.Equal(2, result.Value.Blocks.Count); // pinned + summary
        var pinnedBlock = result.Value.Blocks.FirstOrDefault(b => b.IsPinned);
        Assert.NotNull(pinnedBlock);
        Assert.Equal("pinned memory", pinnedBlock.Content);
    }

    [Fact]
    public async Task CompressAsync_NoEconomyModel_ReturnsPassthrough()
    {
        var mockRouter = new Mock<IModelRouter>();
        mockRouter
            .Setup(r =>
                r.ResolveAsync(
                    AiFeature.MemoryExtraction,
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RouteDecision>.Failure(
                    new Error(DomainErrorCodes.Llm.NoModelForFeature, "no model")
                )
            );

        var blocks = new List<CacheBlock> { new(new string('x', 500), CacheBlockType.Memory) };
        var compressor = CreateCompressor(mockRouter.Object);

        var result = await compressor.CompressAsync(blocks, modelMaxTokens: 100);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.WasCompressed);
        Assert.Single(result.Value.Blocks);
    }

    [Fact]
    public async Task CompressAsync_EmptyBlocks_ReturnsNoOp()
    {
        var mockRouter = new Mock<IModelRouter>();
        var blocks = new List<CacheBlock>();
        var compressor = CreateCompressor(mockRouter.Object);

        var result = await compressor.CompressAsync(blocks, modelMaxTokens: 100);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.WasCompressed);
        Assert.Empty(result.Value.Blocks);
    }

    [Fact]
    public async Task CompressAsync_ThresholdRatioConfig_Respected()
    {
        var mockClient = new Mock<ILlmClient>();
        var summaryChunks = new List<ChatChunk> { new ChatChunk("summary", null, null) };
        mockClient
            .Setup(c => c.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableChunkList(summaryChunks));

        var mockFactory = new Mock<ILlmClientFactory>();
        mockFactory.Setup(f => f.For(It.IsAny<LlmProvider>())).Returns(mockClient.Object);

        var mockRouter = new Mock<IModelRouter>();
        var routeDecision = new RouteDecision(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "economy-model",
                "economy-model-name",
                "Economy",
                0.001m,
                4096,
                true
            ),
            new ProviderDto(
                Guid.NewGuid(),
                "TestProvider",
                "https://test.com",
                "OpenAiCompatible",
                "Text",
                "Economy",
                null,
                true,
                DateTime.UtcNow,
                DateTime.UtcNow
            ),
            [],
            Provider: null
        );
        mockRouter
            .Setup(r =>
                r.ResolveAsync(
                    AiFeature.MemoryExtraction,
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RouteDecision>.Success(routeDecision));

        var blocks = new List<CacheBlock> { new(new string('x', 500), CacheBlockType.Memory) };

        var options = new LlmOptions { ContextCompressionThresholdRatio = 0.01 };
        var compressor = CreateCompressor(mockRouter.Object, mockFactory.Object, options);

        var result = await compressor.CompressAsync(blocks, modelMaxTokens: 10000);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.WasCompressed);
    }

    [Fact]
    public async Task CompressAsync_ErrorChunk_ReturnsPassthrough()
    {
        var mockClient = new Mock<ILlmClient>();
        var errorChunks = new List<ChatChunk>
        {
            new("partial ", null, null),
            new(null, ChatChunkFinishReason.Error, null),
        };
        mockClient
            .Setup(c => c.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableChunkList(errorChunks));

        var mockFactory = new Mock<ILlmClientFactory>();
        mockFactory.Setup(f => f.For(It.IsAny<LlmProvider>())).Returns(mockClient.Object);

        var mockRouter = new Mock<IModelRouter>();
        var routeDecision = new RouteDecision(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "economy-model",
                "economy-model-name",
                "Economy",
                0.001m,
                4096,
                true
            ),
            new ProviderDto(
                Guid.NewGuid(),
                "TestProvider",
                "https://test.com",
                "OpenAiCompatible",
                "Text",
                "Economy",
                null,
                true,
                DateTime.UtcNow,
                DateTime.UtcNow
            ),
            [],
            Provider: null
        );
        mockRouter
            .Setup(r =>
                r.ResolveAsync(
                    AiFeature.MemoryExtraction,
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RouteDecision>.Success(routeDecision));

        var blocks = new List<CacheBlock>
        {
            new(new string('x', 500), CacheBlockType.Memory),
            new(new string('y', 500), CacheBlockType.Memory),
        };
        var compressor = CreateCompressor(mockRouter.Object, mockFactory.Object);

        var result = await compressor.CompressAsync(blocks, modelMaxTokens: 100);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.WasCompressed);
        Assert.Equal(2, result.Value.Blocks.Count);
    }

    private static async IAsyncEnumerable<ChatChunk> AsyncEnumerableChunkList(
        IReadOnlyList<ChatChunk> chunks
    )
    {
        foreach (var chunk in chunks)
            yield return chunk;
        await Task.CompletedTask;
    }
}
