using HydraForge.Application.Admin;
using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Application.Settings;
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
    ProjectService projectService,
    ISettingsRepository settingsRepo,
    ISettingsProvider settingsProvider,
    IAuditLogReader auditLogReader
) : ControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType(typeof(UserListPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default
    )
    {
        var result = await adminService.ListUsersAsync(skip, take, search, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid userId, CancellationToken ct)
    {
        var result = await adminService.GetUserAsync(userId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpPost("users")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken ct
    )
    {
        var result = await adminService.CreateUserAsync(request, ct);
        return result.IsFailure
            ? this.ToProblemResult(result.Error)
            : CreatedAtAction(nameof(GetUser), new { userId = result.Value.Id }, result.Value);
    }

    [HttpPatch("users/{userId:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableUser(Guid userId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await adminService.DisableUserAsync(actorId, userId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : NoContent();
    }

    [HttpPatch("users/{userId:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableUser(Guid userId, CancellationToken ct)
    {
        var result = await adminService.EnableUserAsync(userId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : NoContent();
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid userId,
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await adminService.ResetPasswordAsync(userId, request.NewPassword, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : NoContent();
    }

    [HttpPatch("users/{userId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleRole(Guid userId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await adminService.ToggleAdminRoleAsync(actorId, userId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : NoContent();
    }

    [HttpGet("projects")]
    [ProducesResponseType(typeof(ProjectListPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListProjects(
        [FromQuery] bool includeArchived = true,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default
    )
    {
        var actorId = User.GetRequiredUserId();
        var result = await projectService.GetAllAsync(
            actorId,
            includeArchived,
            search,
            ProjectSortField.Name,
            false,
            null,
            skip,
            take,
            isAdmin: true,
            excludeMembership: false,
            ct
        );
        return result.IsFailure ? this.ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet("projects/{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(Guid projectId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await projectService.GetByIdAsync(projectId, actorId, ct);
        return result.IsFailure ? this.ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await settingsProvider.GetAsync(ct);
        return Ok(
            new
            {
                settings.ArchivedItemRetentionDays,
                settings.AuditLogRetentionDays,
                settings.NotificationRetentionDays,
                settings.NtfyServerUrl,
                settings.SearXngUrl,
                settings.BrandName,
                settings.BrandLogoUrl,
            }
        );
    }

    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateSystemSettingsRequest request,
        CancellationToken ct
    )
    {
        var settings = await settingsRepo.GetSingletonAsync(ct);
        settings.UpdateSettings(
            request.ArchivedItemRetentionDays,
            request.AuditLogRetentionDays,
            request.NotificationRetentionDays,
            request.NtfyServerUrl,
            request.SearXngUrl,
            request.BrandName,
            request.BrandLogoUrl
        );
        await settingsRepo.UpdateAsync(settings, ct);
        settingsProvider.Invalidate();
        return Ok(new { message = "Settings updated. Changes apply within 5 minutes." });
    }

    [HttpGet("audit-log")]
    [ProducesResponseType(typeof(AuditLogQueryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAuditLog(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? actorId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default
    )
    {
        var query = new AuditLogQuery(
            projectId,
            actorId,
            entityType,
            action,
            from,
            to,
            skip,
            Math.Min(take, 500)
        );
        var result = await auditLogReader.QueryAsync(query, ct);
        return Ok(result);
    }
}

public record UpdateSystemSettingsRequest(
    int? ArchivedItemRetentionDays,
    int? AuditLogRetentionDays,
    int? NotificationRetentionDays,
    string? NtfyServerUrl,
    string? SearXngUrl,
    string? BrandName,
    string? BrandLogoUrl
);

public record ResetPasswordRequest(string NewPassword);
