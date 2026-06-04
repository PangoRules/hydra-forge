using HydraForge.Application.Health;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Health;

public class DatabaseHealthProbe(HydraForgeDbContext db) : IHealthProbe
{
    public async Task<HealthStatus> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(ct))
                return HealthStatus.Unhealthy;

            var pending = await db.Database.GetPendingMigrationsAsync(ct);
            if (pending.Any())
                return HealthStatus.Degraded;

            return HealthStatus.Healthy;
        }
        catch
        {
            return HealthStatus.Unhealthy;
        }
    }
}

