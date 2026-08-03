namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfChatSessionDocumentRepository(HydraForgeDbContext context)
    : IChatSessionDocumentRepository
{
    public async Task<IReadOnlyList<ChatSessionDocument>> GetBySessionAsync(
        Guid sessionId,
        CancellationToken ct = default
    )
    {
        return await context
            .ChatSessionDocuments.Where(sd => sd.SessionId == sessionId)
            .OrderByDescending(sd => sd.AddedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ChatSessionDocument sessionDocument, CancellationToken ct = default)
    {
        context.ChatSessionDocuments.Add(sessionDocument);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid sessionId, Guid documentId, CancellationToken ct = default)
    {
        var link = await context.ChatSessionDocuments.FirstOrDefaultAsync(
            sd => sd.SessionId == sessionId && sd.DocumentId == documentId,
            ct
        );
        if (link != null)
        {
            context.ChatSessionDocuments.Remove(link);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(
        Guid sessionId,
        Guid documentId,
        CancellationToken ct = default
    )
    {
        return await context.ChatSessionDocuments.AnyAsync(
            sd => sd.SessionId == sessionId && sd.DocumentId == documentId,
            ct
        );
    }
}
