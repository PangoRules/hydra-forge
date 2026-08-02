using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Document document, CancellationToken ct = default);
    Task ArchiveAsync(Guid documentId, CancellationToken ct = default);
}
