using HydraForge.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(LoginUserHandler loginUserHandler) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await loginUserHandler.HandleAsync(request);
        if (result.IsFailure)
        {
            return Unauthorized(new { error = result.Error.Code, message = result.Error.Message });
        }
        return Ok(result.Value);
    }
}