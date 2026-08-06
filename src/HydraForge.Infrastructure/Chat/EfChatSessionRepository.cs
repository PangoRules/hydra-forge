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
        Guid? beforeId,
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
            query = query.Where(s =>
                s.UpdatedAt < before.Value || (s.UpdatedAt == before.Value && s.Id < beforeId)
            );

        return await query
            .OrderByDescending(s => s.UpdatedAt)
            .ThenByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        Guid ownerId,
        Guid? folderId,
        Guid? projectId,
        CancellationToken ct = default
    )
    {
        var query = context.ChatSessions.Where(s => s.OwnerId == ownerId && s.ArchivedAt == null);

        if (folderId.HasValue)
            query = query.Where(s => s.FolderId == folderId.Value);
        if (projectId.HasValue)
            query = query.Where(s => s.ProjectId == projectId.Value);

        return await query.CountAsync(ct);
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

    public async Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
        Guid ownerId,
        string query,
        Guid? projectId,
        int limit,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var q = context
            .ChatSessions.Where(s => s.OwnerId == ownerId && s.ArchivedAt == null)
            .Where(s => EF.Functions.ILike(s.Title, $"%{query}%"));

        if (projectId.HasValue)
            q = q.Where(s => s.ProjectId == projectId.Value);

        return await q.Take(limit).ToListAsync(ct);
    }

    public async Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default)
    {
        context.CardChatLinks.Add(link);
        await context.SaveChangesAsync(ct);
    }
}
