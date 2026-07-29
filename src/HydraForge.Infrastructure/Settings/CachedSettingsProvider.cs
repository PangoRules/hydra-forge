using HydraForge.Application.Settings;
using HydraForge.Domain.Entities.PersonalSpace;
using Microsoft.Extensions.Caching.Memory;

namespace HydraForge.Infrastructure.Settings;

public class CachedSettingsProvider(ISettingsRepository repo, IMemoryCache cache) : ISettingsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CacheKey = "system_settings";

    public async Task<SystemSettings> GetAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await repo.GetSingletonAsync(ct);
        }) ?? new SystemSettings();
    }

    public void Invalidate()
    {
        cache.Remove(CacheKey);
    }
}
