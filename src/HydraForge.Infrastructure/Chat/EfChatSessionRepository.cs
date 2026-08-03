namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfChatSessionRepository(HydraForgeDbContext context) : IChatSessionRepository
{
    public async Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task<ChatSession?> GetActiveByPanelAsync(
        Guid projectId,
        Guid? openCardId,
        Guid ownerId,
        CancellationToken ct = default
    )
    {
        return await context.ChatSessions.FirstOrDefaultAsync(
            s =>
                s.OwnerId == ownerId
                && s.ProjectId == projectId
                && s.OpenCardId == openCardId
                && s.Status == ChatSessionStatus.Active,
            ct
        );
    }

    public async Task<IReadOnlyList<ChatSession>> ListAsync(
        Guid ownerId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        int limit,
        CancellationToken ct = default
    )
    {
        var query = context.ChatSessions.Where(s => s.OwnerId == ownerId && s.ArchivedAt == null);

        if (folderId.HasValue)
            query = query.Where(s => s.FolderId == folderId.Value);
        if (projectId.HasValue)
            query = query.Where(s => s.ProjectId == projectId.Value);
        if (before.HasValue)
            query = query.Where(s => s.CreatedAt < before.Value);

        return await query.OrderByDescending(s => s.UpdatedAt).Take(limit).ToListAsync(ct);
    }

    public async Task AddAsync(ChatSession session, CancellationToken ct = default)
    {
        context.ChatSessions.Add(session);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ChatSession session, CancellationToken ct = default)
    {
        context.ChatSessions.Update(session);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default)
    {
        context.CardChatLinks.Add(link);
        await context.SaveChangesAsync(ct);
    }
}
