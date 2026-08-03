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

    public async Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
        Guid ownerId,
        string query,
        Guid? projectId,
        int limit,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var sessionIds = context.ChatSessions
            .Where(s => s.OwnerId == ownerId)
            .Where(s => s.ArchivedAt == null)
            .Where(s => !projectId.HasValue || s.ProjectId == projectId.Value)
            .Select(s => s.Id);

        return await context.ChatMessages
            .Where(m => sessionIds.Contains(m.SessionId))
            .Where(m => EF.Functions.ILike(m.Content, $"%{query}%"))
            .Take(limit)
            .ToListAsync(ct);
    }
}
