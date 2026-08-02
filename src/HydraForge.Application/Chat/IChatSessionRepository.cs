using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public interface IChatSessionRepository
{
    Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<ChatSession?> GetActiveByPanelAsync(
        Guid projectId,
        Guid? openCardId,
        Guid ownerId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChatSession>> ListAsync(
        Guid ownerId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        int limit,
        CancellationToken ct = default
    );
    Task AddAsync(ChatSession session, CancellationToken ct = default);
    Task UpdateAsync(ChatSession session, CancellationToken ct = default);
}
