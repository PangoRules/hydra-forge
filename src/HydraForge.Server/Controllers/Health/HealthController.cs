using HydraForge.Application.Health;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Health;

[ApiController]
[Route("[controller]")]
public class HealthController(GetHealthHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var result = await handler.HandleAsync(ct);

        if (!result.IsSuccess)
        {
            return Problem(
                statusCode: 503,
                title: "Service unavailable",
                detail: "Health check failed"
            );
        }

        var response = result.Value;
        var httpStatus = response.OverallStatus == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return new ObjectResult(new
        {
            status = response.OverallStatus.ToString().ToLowerInvariant(),
            components = new[]
            {
                new { name = "server", status = response.ServerStatus.ToString().ToLowerInvariant(), detail = "Server is running." },
                new { name = "database", status = response.DatabaseStatus.ToString().ToLowerInvariant(), detail = response.DatabaseStatus == HealthStatus.Healthy ? "Database is connected." : "Database issue detected." },
                new { name = "llmProviders", status = response.LlmStatus.ToString().ToLowerInvariant(), detail = response.LlmStatus == HealthStatus.NotConfigured ? "No LLM providers configured." : "LLM providers available." }
            }
        })
        { StatusCode = httpStatus };
    }
}