using HydraForge.Application.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController(LoginUserHandler loginUserHandler) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await loginUserHandler.HandleAsync(request);
        if (result.IsFailure)
        {
            var correlationId = HttpContext.Items["CorrelationId"] as string ?? HttpContext.TraceIdentifier;
            var problemDetails = ProblemDetailsMapper.FromError(result.Error, correlationId);
            return new ObjectResult(problemDetails)
            {
                StatusCode = problemDetails.Status,
                ContentTypes = { "application/problem+json" },
            };
        }
        return Ok(result.Value);
    }
}
