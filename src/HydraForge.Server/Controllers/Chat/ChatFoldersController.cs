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
[Route("api/chat/folders")]
public class ChatFoldersController(IChatFolderService folderService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ChatFolderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateChatFolderRequest request)
    {
        var userId = User.GetRequiredUserId();
        var result = await folderService.CreateAsync(request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return CreatedAtAction(nameof(GetById), new { folderId = result.Value.Id }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChatFolderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? projectId = null)
    {
        var userId = User.GetRequiredUserId();
        var result = await folderService.ListAsync(userId, projectId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpGet("{folderId:guid}")]
    [ProducesResponseType(typeof(ChatFolderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid folderId)
    {
        var userId = User.GetRequiredUserId();
        var result = await folderService.ListAsync(userId, projectId: null);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var folder = result.Value.FirstOrDefault(f => f.Id == folderId);
        if (folder is null)
            return this.ToProblemResult(
                new Domain.Common.Error(
                    DomainErrorCodes.Chat.FolderNotFound,
                    "Folder not found."
                )
            );

        return Ok(folder);
    }

    [HttpPatch("{folderId:guid}")]
    [ProducesResponseType(typeof(ChatFolderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid folderId,
        [FromBody] UpdateChatFolderRequest request
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await folderService.UpdateAsync(folderId, request, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("{folderId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid folderId)
    {
        var userId = User.GetRequiredUserId();
        var result = await folderService.ArchiveAsync(folderId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }
}
