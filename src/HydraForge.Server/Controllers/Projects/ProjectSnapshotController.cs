using HydraForge.Application.Projects;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Domain.Common;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/[controller]")]
public class ProjectSnapshotController(
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectMemberRepository memberRepo
) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ProjectSnapshotResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshot(Guid projectId, CancellationToken ct)
    {
        if (!await User.IsProjectMemberOrAdmin(memberRepo, projectId, ct))
        {
            return this.ToProblemResult(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );
        }

        var snapshot = await snapshotRefresher.GetSnapshotAsync(projectId, ct);
        if (snapshot == null)
        {
            return this.ToProblemResult(
                new Error(DomainErrorCodes.Projects.NotFound, "Snapshot not found.")
            );
        }

        return Ok(
            new ProjectSnapshotResponse(
                snapshot.Id,
                snapshot.ProjectId,
                snapshot.TemplateContent,
                snapshot.TemplateGeneratedAt,
                snapshot.AiNarrative,
                snapshot.AiNarrativeGeneratedAt
            )
        );
    }
}

public record ProjectSnapshotResponse(
    Guid Id,
    Guid ProjectId,
    string TemplateContent,
    DateTime TemplateGeneratedAt,
    string? AiNarrative,
    DateTime? AiNarrativeGeneratedAt
);
