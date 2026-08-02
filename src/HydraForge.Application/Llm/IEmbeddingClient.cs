using HydraForge.Domain.Common;

namespace HydraForge.Application.Llm;

public interface IEmbeddingClient
{
    Task<Result<EmbeddingResult>> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken ct = default
    );
}
