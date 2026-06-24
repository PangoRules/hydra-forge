using HydraForge.Application.Cards;
using HydraForge.Application.Auth;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/cards/{cardId:guid}/[controller]")]
public class CardRelationshipsController(CardRelationshipService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();
        var result = await service.ListAsync(projectId, cardId, userId);
        if (result.IsFailure)
            return this.ToProblemResult(result.Error);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId,
        Guid cardId,
        [FromBody] CreateRelationshipRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var cmd = new CreateRelationshipCommand(
            projectId,
            cardId,
            request.TargetCardId,
            request.Type,
            userId
        );
        var result = await service.CreateAsync(cmd);
        if (result.IsFailure)
            return this.ToProblemResult(result.Error);
        return CreatedAtAction(
            nameof(List),
            new { projectId, cardId },
            result.Value
        );
    }

    [HttpDelete("{relationshipId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid cardId, Guid relationshipId)
    {
        var userId = User.GetRequiredUserId();
        var cmd = new DeleteRelationshipCommand(projectId, cardId, relationshipId, userId);
        var result = await service.DeleteAsync(cmd);
        if (result.IsFailure)
            return this.ToProblemResult(result.Error);
        return NoContent();
    }

    [HttpGet("archive-impact")]
    public async Task<IActionResult> ArchiveImpact(Guid projectId, Guid cardId, [FromQuery] bool confirm = false)
    {
        var userId = User.GetRequiredUserId();
        var cmd = new ArchiveImpactCommand(projectId, cardId, confirm, userId);
        var result = await service.GetArchiveImpactAsync(cmd);
        if (result.IsFailure)
            return this.ToProblemResult(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("archive-with-relationships")]
    public async Task<IActionResult> ArchiveWithRelationships(
        Guid projectId,
        Guid cardId,
        [FromBody] ArchiveImpactRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var cmd = new ArchiveImpactCommand(projectId, cardId, request.Confirm, userId);
        var result = await service.ArchiveCardWithRelationshipsAsync(cmd);
        if (result.IsFailure)
            return this.ToProblemResult(result.Error);
        return Ok(result.Value);
    }
}
