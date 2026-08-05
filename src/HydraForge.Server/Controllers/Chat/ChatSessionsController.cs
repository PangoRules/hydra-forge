using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Chat;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/chat/sessions")]
public class ChatSessionsController(IChatSessionService sessionService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateChatSessionRequest request)
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.CreateAsync(request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return CreatedAtAction(nameof(GetById), new { sessionId = result.Value.Id }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ChatSessionPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? folderId,
        [FromQuery] Guid? projectId,
        [FromQuery] DateTime? before = null,
        [FromQuery] Guid? beforeId = null,
        [FromQuery] int limit = 20
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.ListAsync(userId, folderId, projectId, before, beforeId, limit);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(ChatSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid sessionId)
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.GetAsync(sessionId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpPatch("{sessionId:guid}")]
    [ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid sessionId,
        [FromBody] UpdateChatSessionRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.UpdateAsync(sessionId, request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpPost("{sessionId:guid}/close")]
    [ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Close(Guid sessionId)
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.CloseAsync(sessionId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid sessionId)
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.ArchiveAsync(sessionId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }

    [HttpPost("{sessionId:guid}/documents")]
    [ProducesResponseType(typeof(ChatSessionDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AttachDocument(
        Guid sessionId,
        [FromBody] AttachDocumentRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.AttachDocumentAsync(
            sessionId,
            request.DocumentId,
            userId
        );

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return CreatedAtAction(nameof(GetById), new { sessionId }, result.Value);
    }

    [HttpGet("{sessionId:guid}/documents")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatSessionDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListDocuments(Guid sessionId)
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.ListDocumentsAsync(sessionId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("{sessionId:guid}/documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DetachDocument(Guid sessionId, Guid documentId)
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.DetachDocumentAsync(sessionId, documentId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }

    [HttpGet("{sessionId:guid}/permission")]
    [ProducesResponseType(typeof(ChatPermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermission(Guid sessionId)
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.GetPermissionAsync(sessionId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }
}

public record AttachDocumentRequest(Guid DocumentId);
