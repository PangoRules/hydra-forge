using HydraForge.Application.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    LoginUserHandler loginUserHandler,
    IUserRepository userRepository,
    IAccessTokenIssuer accessTokenIssuer
) : ControllerBase
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

    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh()
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var user = await userRepository.FindByIdAsync(userId);
        if (user == null)
            return Unauthorized();

        var token = accessTokenIssuer.IssueToken(user);
        return Ok(new RefreshTokenResponse(token.Value, token.ExpiresAt));
    }
}
