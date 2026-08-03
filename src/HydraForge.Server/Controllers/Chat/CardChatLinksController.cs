using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Chat;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api")]
public class CardChatLinksController(ICardChatLinkService linkService) : ControllerBase
{
    [HttpGet("cards/{cardId:guid}/chat-links")]
    [ProducesResponseType(typeof(IReadOnlyList<CardChatLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCard(Guid cardId)
    {
        var userId = User.GetRequiredUserId();
        var result = await linkService.GetByCardAsync(cardId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("chat/card-links/{linkId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid linkId)
    {
        var userId = User.GetRequiredUserId();
        var result = await linkService.ArchiveAsync(linkId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }
}
