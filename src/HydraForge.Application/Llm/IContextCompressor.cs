using HydraForge.Domain.Common;

namespace HydraForge.Application.Llm;

public interface IContextCompressor
{
    /// <remarks>
    /// Blocks are assumed to be in chronological order (oldest first).
    /// The compressor selects the oldest non-pinned Memory blocks for summarization.
    /// </remarks>
    Task<Result<CompressedContext>> CompressAsync(
        IReadOnlyList<CacheBlock> blocks,
        int modelMaxTokens,
        CancellationToken ct = default
    );
}
