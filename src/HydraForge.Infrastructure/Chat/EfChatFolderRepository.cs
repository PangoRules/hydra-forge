namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfChatFolderRepository(HydraForgeDbContext context) : IChatFolderRepository
{
    public async Task<ChatFolder?> GetByIdAsync(Guid folderId, CancellationToken ct = default)
    {
        return await context.ChatFolders.FirstOrDefaultAsync(f => f.Id == folderId, ct);
    }

    public async Task<IReadOnlyList<ChatFolder>> ListByOwnerAsync(
        Guid ownerId,
        Guid? projectId,
        CancellationToken ct = default
    )
    {
        var query = context.ChatFolders.Where(f => f.OwnerId == ownerId && f.ArchivedAt == null);

        if (projectId.HasValue)
            query = query.Where(f => f.ProjectId == projectId.Value);

        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(ChatFolder folder, CancellationToken ct = default)
    {
        context.ChatFolders.Add(folder);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ChatFolder folder, CancellationToken ct = default)
    {
        context.ChatFolders.Update(folder);
        await context.SaveChangesAsync(ct);
    }
}
