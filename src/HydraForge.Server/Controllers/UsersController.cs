using HydraForge.Application.Auth;
using HydraForge.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepo;

    public UsersController(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 10)
    {
        // Null/empty → return first N users (initial load on focus)
        if (string.IsNullOrWhiteSpace(q))
        {
            var allUsers = await _userRepo.SearchByUsernameAsync("", limit);
            var result = allUsers.Select(u => new { u.Id, u.Username }).ToList();
            return Ok(result);
        }

        var users = await _userRepo.SearchByUsernameAsync(q, limit);
        var result2 = users.Select(u => new { u.Id, u.Username }).ToList();
        return Ok(result2);
    }
}