using HydraForge.Application.Settings;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Settings;

public class EfSettingsRepository(HydraForgeDbContext db) : ISettingsRepository
{
    public async Task<SystemSettings> GetSingletonAsync(CancellationToken ct = default)
    {
        return await db.SystemSettings.FirstOrDefaultAsync(ct) ?? new SystemSettings();
    }

    public async Task UpdateAsync(SystemSettings settings, CancellationToken ct = default)
    {
        db.SystemSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
