using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public interface IChatMessageRepository
{
    Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
        Guid sessionId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
        Guid ownerId,
        string query,
        Guid? projectId,
        int limit,
        bool isAdmin = false,
        ChatSessionScope scope = ChatSessionScope.Mine,
        CancellationToken ct = default
    );
    Task AddAsync(ChatMessage message, CancellationToken ct = default);

    // Deletes messageId and every message chronologically after it in the session.
    Task<bool> DeleteFromAsync(Guid sessionId, Guid messageId, CancellationToken ct = default);
}
