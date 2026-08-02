using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public interface IChatRagRetriever
{
    Task<IReadOnlyList<CacheBlock>> RetrieveAsync(
        Guid sessionId,
        string query,
        bool searchAllMyDocs,
        string? presetContent,
        int k,
        CancellationToken ct = default
    );
}
