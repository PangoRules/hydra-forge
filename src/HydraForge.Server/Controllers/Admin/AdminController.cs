using HydraForge.Application.Admin;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Domain.Enums;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Admin;

[Authorize(Policy = AuthPolicies.AdminRequired)]
[ApiController]
[Route("api/admin")]
public class AdminController(
    IAdminService adminService,
    ProjectService projectService
) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await adminService.ListUsersAsync(skip, take, search, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUser(Guid userId, CancellationToken ct)
    {
        var result = await adminService.GetUserAsync(userId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await adminService.CreateUserAsync(request, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : CreatedAtAction(nameof(GetUser), new { userId = result.Value.Id }, result.Value);
    }

    [HttpPatch("users/{userId:guid}/disable")]
    public async Task<IActionResult> DisableUser(Guid userId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await adminService.DisableUserAsync(actorId, userId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : NoContent();
    }

    [HttpPatch("users/{userId:guid}/enable")]
    public async Task<IActionResult> EnableUser(Guid userId, CancellationToken ct)
    {
        var result = await adminService.EnableUserAsync(userId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : NoContent();
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await adminService.ResetPasswordAsync(userId, request.NewPassword, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : NoContent();
    }

    [HttpPatch("users/{userId:guid}/role")]
    public async Task<IActionResult> ToggleRole(Guid userId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await adminService.ToggleAdminRoleAsync(actorId, userId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : NoContent();
    }

    [HttpGet("projects")]
    public async Task<IActionResult> ListProjects(
        [FromQuery] bool includeArchived = true,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var actorId = User.GetRequiredUserId();
        var result = await projectService.GetAllAsync(
            actorId, includeArchived, search, ProjectSortField.Name, false, null, skip, take, isAdmin: true, excludeMembership: false, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<IActionResult> GetProject(Guid projectId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await projectService.GetByIdAsync(projectId, actorId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : Ok(result.Value);
    }
}

public record ResetPasswordRequest(string NewPassword);
