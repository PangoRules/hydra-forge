using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Llm;

public interface ILlmClient
{
    AdapterType AdapterType { get; }

    IAsyncEnumerable<ChatChunk> StreamChatAsync(ChatRequest request, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ProviderModelDto>>> GetModelsAsync(CancellationToken ct = default);

    bool SupportsToolCalling(ProviderModelConfigDto model);
}
