namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfPromptPresetRepository(HydraForgeDbContext context) : IPromptPresetRepository
{
    public async Task<PromptPreset?> GetByIdAsync(Guid presetId, CancellationToken ct = default)
    {
        return await context.PromptPresets.FirstOrDefaultAsync(p => p.Id == presetId, ct);
    }

    public async Task<IReadOnlyList<PromptPreset>> ListByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.PromptPresets.Where(p => p.UserId == userId && p.ArchivedAt == null).ToListAsync(ct);
    }

    public async Task AddAsync(PromptPreset preset, CancellationToken ct = default)
    {
        context.PromptPresets.Add(preset);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PromptPreset preset, CancellationToken ct = default)
    {
        context.PromptPresets.Update(preset);
        await context.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid presetId, CancellationToken ct = default)
    {
        var preset = await context.PromptPresets.FindAsync([presetId], ct);
        if (preset is not null)
        {
            preset.ArchivedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task NullifyGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var presets = await context.PromptPresets.Where(p => p.GroupId == groupId).ToListAsync(ct);
        foreach (var p in presets) p.GroupId = null;
        await context.SaveChangesAsync(ct);
    }
}
