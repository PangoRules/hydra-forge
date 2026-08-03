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
[Route("api/chat/presets")]
public class PromptPresetsController(IPromptPresetService presetService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PromptPresetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePromptPresetRequest request)
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.CreatePresetAsync(request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return CreatedAtAction(
            nameof(GetById),
            new { presetId = result.Value.Id },
            result.Value
        );
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PromptPresetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? groupId = null)
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.ListPresetsAsync(userId, groupId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpGet("{presetId:guid}")]
    [ProducesResponseType(typeof(PromptPresetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid presetId)
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.GetPresetByIdAsync(presetId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpPatch("{presetId:guid}")]
    [ProducesResponseType(typeof(PromptPresetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid presetId,
        [FromBody] UpdatePromptPresetRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.UpdatePresetAsync(presetId, request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("{presetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid presetId)
    {
        var userId = User.GetRequiredUserId();
        var result = await presetService.ArchivePresetAsync(presetId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }
}
