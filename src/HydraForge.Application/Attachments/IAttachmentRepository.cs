using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.Attachments;

public interface IAttachmentRepository
{
    Task<Attachment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Attachment>> ListByCardAsync(Guid cardId, CancellationToken ct = default);
    Task AddAsync(Attachment attachment, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}