using HydraForge.Domain.Common;

namespace HydraForge.Application.Chat;

public interface IDocumentService
{
    Task<Result<DocumentDto>> GetByIdAsync(Guid documentId, Guid actorId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DocumentDto>>> ListAsync(Guid actorId, string? q = null, CancellationToken ct = default);
    Task<Result> ArchiveAsync(Guid documentId, Guid actorId, CancellationToken ct = default);
}
