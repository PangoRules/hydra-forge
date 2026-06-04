namespace HydraForge.Application.Health;

public interface IHealthProbe
{
    Task<HealthStatus> CheckAsync(CancellationToken ct = default);
}
