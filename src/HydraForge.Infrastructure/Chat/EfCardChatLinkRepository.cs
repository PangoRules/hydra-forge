namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfCardChatLinkRepository(HydraForgeDbContext context)
    : ICardChatLinkRepository
{
    public async Task<CardChatLink?> GetByIdAsync(Guid linkId, CancellationToken ct = default)
    {
        return await context.CardChatLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct);
    }

    public async Task<IReadOnlyList<CardChatLink>> GetByCardAsync(Guid cardId, CancellationToken ct = default)
    {
        return await context.CardChatLinks
            .Where(l => l.CardId == cardId && l.ArchivedAt == null)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CardChatLink link, CancellationToken ct = default)
    {
        context.CardChatLinks.Add(link);
        await context.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid linkId, CancellationToken ct = default)
    {
        var link = await context.CardChatLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct);
        if (link != null)
        {
            link.ArchivedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }
    }
}
