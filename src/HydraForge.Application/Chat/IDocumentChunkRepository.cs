using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public interface IDocumentChunkRepository
{
    Task AddAsync(DocumentChunk chunk, CancellationToken ct = default);
    Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        Guid userId,
        IReadOnlyList<Guid>? sessionDocumentIds,
        ReadOnlyMemory<float> queryEmbedding,
        int k,
        CancellationToken ct = default
    );
}
