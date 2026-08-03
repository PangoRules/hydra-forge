using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Domain.Common;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Chat;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/chat/preset-groups")]
public class PromptPresetGroupsController(IPromptPresetService presetService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PromptPresetGroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePromptPresetGroupRequest request)
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.CreateGroupAsync(request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return CreatedAtAction(nameof(GetById), new { groupId = result.Value.Id }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PromptPresetGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.ListGroupsAsync(userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpGet("{groupId:guid}")]
    [ProducesResponseType(typeof(PromptPresetGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid groupId)
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.ListGroupsAsync(userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var group = result.Value.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
            return this.ToProblemResult(
                new Domain.Common.Error(
                    DomainErrorCodes.Chat.PresetGroupNotFound,
                    "Group not found."
                )
            );

        return Ok(group);
    }

    [HttpPatch("{groupId:guid}")]
    [ProducesResponseType(typeof(PromptPresetGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid groupId,
        [FromBody] UpdatePromptPresetGroupRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.UpdateGroupAsync(groupId, request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("{groupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid groupId)
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.ArchiveGroupAsync(groupId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }
}
