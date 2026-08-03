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
    Task AddAsync(ChatMessage message, CancellationToken ct = default);
}
