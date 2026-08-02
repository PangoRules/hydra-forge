using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public interface IChatSessionDocumentRepository
{
    Task<IReadOnlyList<ChatSessionDocument>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task AddAsync(ChatSessionDocument sessionDocument, CancellationToken ct = default);
    Task RemoveAsync(Guid sessionId, Guid documentId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid sessionId, Guid documentId, CancellationToken ct = default);
}
