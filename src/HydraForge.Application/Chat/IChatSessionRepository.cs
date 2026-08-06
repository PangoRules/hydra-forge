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
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed,
        CancellationToken ct = default
    );

    // Total count of sessions matching the same filter as ListAsync (owner +
    // folder + project + not archived) but WITHOUT the cursor — used by the
    // service to return a real TotalCount so the client can tell whether there
    // are more pages to load. ListAsync's own result count is just the current
    // page size, which is always <= limit and tells the client nothing about
    // what's beyond the cursor.
    Task<int> CountAsync(
        Guid ownerId,
        Guid? folderId,
        Guid? projectId,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed,
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
