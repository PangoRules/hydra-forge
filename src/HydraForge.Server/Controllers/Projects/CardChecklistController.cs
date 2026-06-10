using HydraForge.Application.Checklist;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/cards/{cardId:guid}/[controller]")]
public class CardChecklistController(ChecklistService checklistService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();
        var result = await checklistService.ListAsync(projectId, cardId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new ChecklistItemListResponse([
            .. result.Value.Select(i => new ChecklistItemResponse(
                i.Id,
                i.CardId,
                i.Text,
                i.IsCompleted,
                i.Position,
                i.AssignedTo,
                i.AssignedToUsername,
                i.CreatedAt
            )),
        ]);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId,
        Guid cardId,
        [FromBody] CreateChecklistItemRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new CreateChecklistItemCommand(
            projectId,
            cardId,
            userId,
            request.Text,
            request.AssignedTo,
            request.Position
        );
        var result = await checklistService.CreateAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new ChecklistItemResponse(
            result.Value.Id,
            result.Value.CardId,
            result.Value.Text,
            result.Value.IsCompleted,
            result.Value.Position,
            result.Value.AssignedTo,
            result.Value.AssignedToUsername,
            result.Value.CreatedAt
        );
        return CreatedAtAction(nameof(List), new { projectId, cardId }, response);
    }

    [HttpPut("{itemId:guid}")]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid cardId,
        Guid itemId,
        [FromBody] UpdateChecklistItemRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new UpdateChecklistItemCommand(
            projectId,
            cardId,
            itemId,
            userId,
            request.Text,
            request.AssignedTo
        );
        var result = await checklistService.UpdateAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new ChecklistItemResponse(
            result.Value.Id,
            result.Value.CardId,
            result.Value.Text,
            result.Value.IsCompleted,
            result.Value.Position,
            result.Value.AssignedTo,
            result.Value.AssignedToUsername,
            result.Value.CreatedAt
        );
        return Ok(response);
    }

    [HttpPatch("{itemId:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid projectId, Guid cardId, Guid itemId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new ToggleChecklistItemCommand(projectId, cardId, itemId, userId);
        var result = await checklistService.ToggleAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new ChecklistItemResponse(
            result.Value.Id,
            result.Value.CardId,
            result.Value.Text,
            result.Value.IsCompleted,
            result.Value.Position,
            result.Value.AssignedTo,
            result.Value.AssignedToUsername,
            result.Value.CreatedAt
        );
        return Ok(response);
    }

    [HttpPut("{itemId:guid}/reorder")]
    public async Task<IActionResult> Reorder(
        Guid projectId,
        Guid cardId,
        Guid itemId,
        [FromBody] ReorderChecklistItemRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new ReorderChecklistItemCommand(
            projectId,
            cardId,
            itemId,
            userId,
            request.NewPosition
        );
        var result = await checklistService.ReorderAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new ChecklistItemResponse(
            result.Value.Id,
            result.Value.CardId,
            result.Value.Text,
            result.Value.IsCompleted,
            result.Value.Position,
            result.Value.AssignedTo,
            result.Value.AssignedToUsername,
            result.Value.CreatedAt
        );
        return Ok(response);
    }

    [HttpDelete("{itemId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid cardId, Guid itemId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new DeleteChecklistItemCommand(projectId, cardId, itemId, userId);
        var result = await checklistService.DeleteAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }
}

