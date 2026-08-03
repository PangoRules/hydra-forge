using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Chat;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/chat/sessions/{sessionId:guid}/messages")]
public class ChatMessagesController(IChatMessageService messageService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ChatMessagePageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        Guid sessionId,
        [FromQuery] DateTime? before = null,
        [FromQuery] Guid? beforeId = null,
        [FromQuery] int limit = 50
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await messageService.GetHistoryAsync(
            sessionId,
            userId,
            before,
            beforeId,
            limit
        );

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        Guid sessionId,
        [FromBody] SendMessageRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await messageService.SendUserMessageAsync(
            sessionId,
            userId,
            request.Content,
            request.Images
        );

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return CreatedAtAction(nameof(GetHistory), new { sessionId }, result.Value);
    }
}

public record SendMessageRequest(
    string Content,
    IReadOnlyList<Application.Llm.ImageBlock>? Images = null
);
