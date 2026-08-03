namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfPromptPresetGroupRepository(HydraForgeDbContext context)
    : IPromptPresetGroupRepository
{
    public async Task<PromptPresetGroup?> GetByIdAsync(Guid groupId, CancellationToken ct = default)
    {
        return await context.PromptPresetGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
    }

    public async Task<IReadOnlyList<PromptPresetGroup>> ListByUserAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await context
            .PromptPresetGroups.Where(g => g.UserId == userId && g.ArchivedAt == null)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PromptPresetGroup group, CancellationToken ct = default)
    {
        context.PromptPresetGroups.Add(group);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PromptPresetGroup group, CancellationToken ct = default)
    {
        context.PromptPresetGroups.Update(group);
        await context.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await context.PromptPresetGroups.FindAsync([groupId], ct);
        if (group is not null)
        {
            group.Archive();
            await context.SaveChangesAsync(ct);
        }
    }
}
