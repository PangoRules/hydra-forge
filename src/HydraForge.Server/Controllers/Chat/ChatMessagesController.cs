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
public class ChatMessagesController(
    IChatMessageService messageService,
    IBackgroundTaskQueue backgroundTaskQueue
) : ControllerBase
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

    /// <summary>
    /// Triggers the AI reply for an already-persisted user message. Plain REST, on purpose —
    /// unlike the SignalR hub's SendMessage, this doesn't need a live WebSocket connection to
    /// even fire (mobile SignalR handshakes have been observed taking 90-170s+ over some
    /// networks, which made "wait for the socket, then trigger" fail outright). The request
    /// only needs to survive long enough to enqueue the Hangfire job — generation itself runs
    /// decoupled from this request's lifetime, so a dropped connection right after doesn't
    /// cancel a reply that would otherwise have completed. Clients pick up the result via
    /// SignalR if connected, or their next REST fetch of the session either way.
    /// </summary>
    [HttpPost("{messageId:guid}/generate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> GenerateReply(
        Guid sessionId,
        Guid messageId,
        [FromBody] GenerateReplyRequest? request
    )
    {
        var userId = User.GetRequiredUserId();
        var presetId = request?.PresetId;
        var preferredProviderModelConfigId = request?.PreferredProviderModelConfigId;
        await backgroundTaskQueue.EnqueueJobAsync<ChatReplyGenerator>(g =>
            g.GenerateAsync(
                sessionId,
                messageId,
                userId,
                presetId,
                preferredProviderModelConfigId,
                CancellationToken.None
            )
        );
        return Accepted();
    }

    [HttpPost("{messageId:guid}/rollback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rollback(Guid sessionId, Guid messageId)
    {
        var userId = User.GetRequiredUserId();
        var result = await messageService.RollbackAsync(sessionId, userId, messageId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }
}

public record SendMessageRequest(
    string Content,
    IReadOnlyList<Application.Llm.ImageBlock>? Images = null
);

public record GenerateReplyRequest(
    Guid? PresetId = null,
    Guid? PreferredProviderModelConfigId = null
);
