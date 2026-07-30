using HydraForge.Domain.Common;

namespace HydraForge.Application.Llm;

public interface IContextCompressor
{
    Task<Result<CompressedContext>> CompressAsync(
        IReadOnlyList<CacheBlock> blocks,
        int modelMaxTokens,
        CancellationToken ct = default);
}
