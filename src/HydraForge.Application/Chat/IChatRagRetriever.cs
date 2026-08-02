using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public interface IChatRagRetriever
{
    Task<IReadOnlyList<DocumentChunk>> RetrieveAsync(
        Guid sessionId,
        ReadOnlyMemory<float> queryEmbedding,
        bool searchAllMyDocs,
        int k,
        CancellationToken ct = default
    );
}
