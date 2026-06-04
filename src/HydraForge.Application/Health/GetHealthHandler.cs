using HydraForge.Domain.Common;

namespace HydraForge.Application.Health;

public class GetHealthHandler(
    IHealthProbe serverProbe,
    IHealthProbe databaseProbe,
    IHealthProbe llmProbe
)
{
    public async Task<Result<GetHealthResponse>> HandleAsync(CancellationToken ct = default)
    {
        var serverStatus = await serverProbe.CheckAsync(ct);
        var databaseStatus = await databaseProbe.CheckAsync(ct);
        var llmStatus = await llmProbe.CheckAsync(ct);

        var overall = ComputeOverall(serverStatus, databaseStatus, llmStatus);

        return Result<GetHealthResponse>.Success(
            new GetHealthResponse(overall, serverStatus, databaseStatus, llmStatus)
        );
    }

    private static HealthStatus ComputeOverall(
        HealthStatus server,
        HealthStatus database,
        HealthStatus llm
    )
    {
        // Critical: server or DB down = overall unhealthy
        if (server == HealthStatus.Unhealthy || database == HealthStatus.Unhealthy)
            return HealthStatus.Unhealthy;

        // LLM NotConfigured is OK — does not fail the system
        if (llm == HealthStatus.NotConfigured)
            return HealthStatus.Healthy;

        // Any other degraded component → degraded
        if (server == HealthStatus.Degraded
            || database == HealthStatus.Degraded
            || llm == HealthStatus.Degraded)
            return HealthStatus.Degraded;

        return HealthStatus.Healthy;
    }
}
