using HydraForge.Application.Projects;
using HydraForge.Application.Auth;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController(
    ProjectService projectService,
    ProjectMemberService projectMemberService
) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new CreateProjectCommand(
            userId,
            request.Name,
            request.Description,
            request.GitRemoteUrl,
            request.GitProvider
        );
        var result = await projectService.CreateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectResponse(
            result.Value.Id,
            result.Value.Name,
            result.Value.Description,
            result.Value.GitRemoteUrl,
            result.Value.GitProvider,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt,
            [
                .. result.Value.Columns.Select(c => new ColumnResponse(
                    c.Id,
                    c.Name,
                    c.Position,
                    c.WipLimit,
                    c.Color
                )),
            ],
            [
                .. result.Value.Members.Select(m => new MemberResponse(
                    m.Id,
                    m.UserId,
                    m.Username,
                    m.Role,
                    m.JoinedAt
                )),
            ]
        );

        return CreatedAtAction(nameof(GetById), new { projectId = result.Value.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ProjectListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var userId = User.GetRequiredUserId();

        var result = await projectService.GetAllAsync(userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = result
            .Value.Select(p => new ProjectListResponse(
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt,
                p.ArchivedAt,
                p.MemberCount
            ))
            .ToList();
        return Ok(response);
    }

    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId)
    {
        var userId = User.GetRequiredUserId();

        var result = await projectService.GetByIdAsync(projectId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectResponse(
            result.Value.Id,
            result.Value.Name,
            result.Value.Description,
            result.Value.GitRemoteUrl,
            result.Value.GitProvider,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt,
            [
                .. result.Value.Columns.Select(c => new ColumnResponse(
                    c.Id,
                    c.Name,
                    c.Position,
                    c.WipLimit,
                    c.Color
                )),
            ],
            [
                .. result.Value.Members.Select(m => new MemberResponse(
                    m.Id,
                    m.UserId,
                    m.Username,
                    m.Role,
                    m.JoinedAt
                )),
            ]
        );

        return Ok(response);
    }

    [HttpPut("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid projectId, [FromBody] UpdateProjectRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new UpdateProjectCommand(
            projectId,
            userId,
            request.Name,
            request.Description,
            request.GitRemoteUrl,
            request.GitProvider
        );
        var result = await projectService.UpdateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectResponse(
            result.Value.Id,
            result.Value.Name,
            result.Value.Description,
            result.Value.GitRemoteUrl,
            result.Value.GitProvider,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt,
            [
                .. result.Value.Columns.Select(c => new ColumnResponse(
                    c.Id,
                    c.Name,
                    c.Position,
                    c.WipLimit,
                    c.Color
                )),
            ],
            [
                .. result.Value.Members.Select(m => new MemberResponse(
                    m.Id,
                    m.UserId,
                    m.Username,
                    m.Role,
                    m.JoinedAt
                )),
            ]
        );

        return Ok(response);
    }

    [HttpDelete("{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new ArchiveProjectCommand(projectId, userId);
        var result = await projectService.ArchiveAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }

    [HttpGet("{projectId:guid}/members")]
    [ProducesResponseType(typeof(List<MemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(Guid projectId)
    {
        var userId = User.GetRequiredUserId();

        var projectResult = await projectService.GetByIdAsync(projectId, userId);
        if (projectResult.IsFailure)
        {
            return this.ToProblemResult(projectResult.Error);
        }

        var response = projectResult
            .Value.Members.Select(m => new MemberResponse(m.Id, m.UserId, m.Username, m.Role, m.JoinedAt))
            .ToList();
        return Ok(response);
    }

    [HttpPost("{projectId:guid}/members")]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMember(Guid projectId, [FromBody] AddMemberRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AddProjectMemberCommand(projectId, request.UserId, request.Role, userId);
        var result = await projectMemberService.AddMemberAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new MemberResponse(
            result.Value.Id,
            result.Value.UserId,
            result.Value.Username,
            result.Value.Role,
            result.Value.JoinedAt
        );
        return CreatedAtAction(nameof(GetById), new { projectId }, response);
    }

    [HttpPut("{projectId:guid}/members/{memberId:guid}")]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMember(
        Guid projectId,
        Guid memberId,
        [FromBody] UpdateMemberRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new UpdateProjectMemberCommand(projectId, memberId, request.Role, userId);
        var result = await projectMemberService.UpdateMemberAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new MemberResponse(
            result.Value.Id,
            result.Value.UserId,
            result.Value.Username,
            result.Value.Role,
            result.Value.JoinedAt
        );
        return Ok(response);
    }

    [HttpDelete("{projectId:guid}/members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid memberId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new RemoveProjectMemberCommand(projectId, memberId, userId);
        var result = await projectMemberService.RemoveMemberAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }
}
