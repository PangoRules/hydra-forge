using HydraForge.Application.Health;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Health;

public class DatabaseHealthProbe : IHealthProbe
{
    private readonly HydraForgeDbContext _db;

    public DatabaseHealthProbe(HydraForgeDbContext db)
    {
        _db = db;
    }

    public async Task<HealthStatus> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            if (!await _db.Database.CanConnectAsync(ct))
                return HealthStatus.Unhealthy;

            var pending = await _db.Database.GetPendingMigrationsAsync(ct);
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