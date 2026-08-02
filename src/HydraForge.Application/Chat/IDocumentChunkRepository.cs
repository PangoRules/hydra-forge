using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public interface IDocumentChunkRepository
{
    Task AddAsync(DocumentChunk chunk, CancellationToken ct = default);
    Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
}
