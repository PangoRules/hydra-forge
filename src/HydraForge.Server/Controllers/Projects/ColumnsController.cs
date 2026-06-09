using HydraForge.Application.Columns;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppColumns = HydraForge.Application.Columns;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/[controller]")]
public class ColumnsController(ColumnService columnService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId)
    {
        var userId = User.GetRequiredUserId();

        var result = await columnService.GetAllByProjectAsync(projectId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = result
            .Value.Select(c => new AppColumns.ColumnResponse(
                c.Id,
                c.Name,
                c.Position,
                c.WipLimit,
                c.Color
            ))
            .ToList();

        return Ok(response);
    }

    [HttpGet("{columnId:guid}")]
    public async Task<IActionResult> GetById(Guid projectId, Guid columnId)
    {
        var userId = User.GetRequiredUserId();

        var result = await columnService.GetByIdAsync(projectId, columnId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppColumns.ColumnResponse(
            result.Value.Id,
            result.Value.Name,
            result.Value.Position,
            result.Value.WipLimit,
            result.Value.Color
        );

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateColumnRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new CreateColumnCommand(
            projectId,
            request.Name,
            request.Color,
            request.WipLimit,
            userId
        );
        var result = await columnService.CreateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppColumns.ColumnResponse(
            result.Value.Id,
            result.Value.Name,
            result.Value.Position,
            result.Value.WipLimit,
            result.Value.Color
        );

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, columnId = result.Value.Id },
            response
        );
    }

    [HttpPut("{columnId:guid}")]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid columnId,
        [FromBody] UpdateColumnRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new UpdateColumnCommand(
            projectId,
            columnId,
            request.Name,
            request.Color,
            request.WipLimit,
            userId
        );
        var result = await columnService.UpdateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppColumns.ColumnResponse(
            result.Value.Id,
            result.Value.Name,
            result.Value.Position,
            result.Value.WipLimit,
            result.Value.Color
        );

        return Ok(response);
    }

    [HttpDelete("{columnId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid columnId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new DeleteColumnCommand(projectId, columnId, userId);
        var result = await columnService.DeleteAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(
        Guid projectId,
        [FromBody] ReorderColumnsRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new ReorderColumnsCommand(projectId, request.ColumnIds, userId);
        var result = await columnService.ReorderAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }
}

