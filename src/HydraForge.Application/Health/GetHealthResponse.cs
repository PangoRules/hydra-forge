namespace HydraForge.Application.Health;

public record GetHealthResponse(
    HealthStatus OverallStatus,
    HealthStatus ServerStatus,
    HealthStatus DatabaseStatus,
    HealthStatus LlmStatus
);
