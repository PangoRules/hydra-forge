using HydraForge.Application.Health;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Health;

public class LlmProviderHealthProbe : IHealthProbe
{
    private readonly HydraForgeDbContext _db;

    public LlmProviderHealthProbe(HydraForgeDbContext db)
    {
        _db = db;
    }

    public async Task<HealthStatus> CheckAsync(CancellationToken ct = default)
    {
        var hasEnabled = await _db.LlmProviders
            .Where(p => p.IsEnabled)
            .AnyAsync(ct);

        return hasEnabled
            ? HealthStatus.Healthy
            : HealthStatus.NotConfigured;
    }
}