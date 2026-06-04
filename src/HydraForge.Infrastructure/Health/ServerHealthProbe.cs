using HydraForge.Application.Health;

namespace HydraForge.Infrastructure.Health;

public class ServerHealthProbe : IHealthProbe
{
    public Task<HealthStatus> CheckAsync(CancellationToken ct = default) =>
        Task.FromResult(HealthStatus.Healthy);
}

