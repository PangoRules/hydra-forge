using HydraForge.Application.Health;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Health;

public class LlmProviderHealthProbe(HydraForgeDbContext db) : IHealthProbe
{
    public async Task<HealthStatus> CheckAsync(CancellationToken ct = default)
    {
        var hasEnabled = await db.LlmProviders.Where(p => p.IsEnabled).AnyAsync(ct);

        return hasEnabled ? HealthStatus.Healthy : HealthStatus.NotConfigured;
    }
}

