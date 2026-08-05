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
        Guid? beforeId,
        int limit,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
        Guid ownerId,
        string query,
        Guid? projectId,
        int limit,
        CancellationToken ct = default
    );
    Task AddAsync(ChatSession session, CancellationToken ct = default);
    Task UpdateAsync(ChatSession session, CancellationToken ct = default);
    Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default);
}
