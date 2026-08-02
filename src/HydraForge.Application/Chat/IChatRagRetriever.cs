using HydraForge.Application.Llm;

namespace HydraForge.Application.Chat;

public interface IChatRagRetriever
{
    Task<IReadOnlyList<CacheBlock>> RetrieveAsync(
        Guid sessionId,
        string query,
        bool searchAllMyDocs,
        int? k,
        CancellationToken ct = default
    );
}
