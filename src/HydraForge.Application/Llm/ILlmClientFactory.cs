using HydraForge.Domain.Entities.Admin;

namespace HydraForge.Application.Llm;

public interface ILlmClientFactory
{
    ILlmClient For(LlmProvider provider);

    ILlmClient For(ProviderDto provider);

    IImageClient ImageFor(LlmProvider provider);

    IEmbeddingClient EmbeddingFor(LlmProvider provider);

    void Invalidate(Guid providerId);
}
