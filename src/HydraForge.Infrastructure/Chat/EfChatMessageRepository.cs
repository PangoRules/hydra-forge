namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfChatMessageRepository(HydraForgeDbContext context) : IChatMessageRepository
{
    public async Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default)
    {
        return await context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
        Guid sessionId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        CancellationToken ct = default
    )
    {
        var query = context.ChatMessages.Where(m => m.SessionId == sessionId);

        if (before.HasValue)
        {
            query = query.Where(m =>
                m.CreatedAt < before.Value
                || (m.CreatedAt == before.Value && beforeId.HasValue && m.Id < beforeId.Value)
            );
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ChatMessage message, CancellationToken ct = default)
    {
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync(ct);
    }
}
