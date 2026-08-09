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
        Guid actorId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        bool isAdmin = false,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
        ChatSessionScope scope = ChatSessionScope.Mine,
        IReadOnlySet<ChatSessionKind>? types = null,
        CancellationToken ct = default
    );

    // Total count of sessions matching the same filter as ListAsync (owner +
    // folder + project + not archived) but WITHOUT the cursor — used by the
    // service to return a real TotalCount so the client can tell whether there
    // are more pages to load. ListAsync's own result count is just the current
    // page size, which is always <= limit and tells the client nothing about
    // what's beyond the cursor.
    Task<int> CountAsync(
        Guid actorId,
        Guid? folderId,
        Guid? projectId,
        bool isAdmin = false,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
        ChatSessionScope scope = ChatSessionScope.Mine,
        IReadOnlySet<ChatSessionKind>? types = null,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
        Guid actorId,
        string query,
        Guid? projectId,
        int limit,
        bool isAdmin = false,
        ChatSessionScope scope = ChatSessionScope.Mine,
        CancellationToken ct = default
    );
    Task AddAsync(ChatSession session, CancellationToken ct = default);
    Task UpdateAsync(ChatSession session, CancellationToken ct = default);
    Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default);

    // Looks up an existing link for a (card, session) pair so LinkCardAsync and
    // CloseAsync can share one row per session instead of each inserting its own —
    // LinkCardAsync creates it eagerly with a placeholder summary so the card's
    // chat-link list shows the session immediately; CloseAsync then fills in the
    // real summary on the same row rather than inserting a duplicate.
    Task<CardChatLink?> FindCardChatLinkAsync(
        Guid cardId,
        Guid chatSessionId,
        CancellationToken ct = default
    );
    Task UpdateCardChatLinkSummaryAsync(
        Guid linkId,
        string summary,
        CancellationToken ct = default
    );
}
