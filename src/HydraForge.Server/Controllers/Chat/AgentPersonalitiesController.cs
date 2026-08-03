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
[Route("api/chat/personalities")]
public class AgentPersonalitiesController(IAgentPersonalityService personalityService)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AgentPersonalityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAgentPersonalityRequest request)
    {
        var userId = User.GetRequiredUserId();
        var result = await personalityService.CreateAsync(request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return CreatedAtAction(
            nameof(GetById),
            new { personalityId = result.Value.Id },
            result.Value
        );
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AgentPersonalityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var userId = User.GetRequiredUserId();
        var result = await personalityService.ListAsync(userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpGet("{personalityId:guid}")]
    [ProducesResponseType(typeof(AgentPersonalityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid personalityId)
    {
        var userId = User.GetRequiredUserId();
        var result = await personalityService.ListAsync(userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var personality = result.Value.FirstOrDefault(p => p.Id == personalityId);
        if (personality is null)
            return this.ToProblemResult(
                new Domain.Common.Error(
                    DomainErrorCodes.Chat.PersonalityNotFound,
                    "Personality not found."
                )
            );

        return Ok(personality);
    }

    [HttpPatch("{personalityId:guid}")]
    [ProducesResponseType(typeof(AgentPersonalityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid personalityId,
        [FromBody] UpdateAgentPersonalityRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await personalityService.UpdateAsync(personalityId, request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("{personalityId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid personalityId)
    {
        var userId = User.GetRequiredUserId();
        var result = await personalityService.ArchiveAsync(personalityId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }

    [HttpPost("{personalityId:guid}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(Guid personalityId)
    {
        var userId = User.GetRequiredUserId();
        var result = await personalityService.SetDefaultAsync(personalityId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }
}
