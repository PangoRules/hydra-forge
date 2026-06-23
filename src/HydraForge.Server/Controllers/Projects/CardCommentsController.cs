using HydraForge.Application.Comments;
using HydraForge.Application.Auth;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/cards/{cardId:guid}/[controller]")]
public class CardCommentsController(CommentService commentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();
        var result = await commentService.ListAsync(projectId, cardId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new CommentListResponse([
            .. result.Value.Select(c => new CommentResponse(
                c.Id,
                c.CardId,
                c.AuthorId,
                c.AuthorUsername,
                c.Content,
                c.CreatedAt,
                c.UpdatedAt,
                c.ArchivedAt,
                c.MentionedUserIds
            )),
        ]);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId,
        Guid cardId,
        [FromBody] CreateCommentRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new CreateCommentCommand(projectId, cardId, userId, request.Content);
        var result = await commentService.CreateAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new CommentResponse(
            result.Value.Id,
            result.Value.CardId,
            result.Value.AuthorId,
            result.Value.AuthorUsername,
            result.Value.Content,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt,
            result.Value.MentionedUserIds
        );
        return CreatedAtAction(nameof(List), new { projectId, cardId }, response);
    }

    [HttpPut("{commentId:guid}")]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid cardId,
        Guid commentId,
        [FromBody] UpdateCommentRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new UpdateCommentCommand(projectId, cardId, commentId, userId, request.Content);
        var result = await commentService.UpdateAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new CommentResponse(
            result.Value.Id,
            result.Value.CardId,
            result.Value.AuthorId,
            result.Value.AuthorUsername,
            result.Value.Content,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt,
            result.Value.MentionedUserIds
        );
        return Ok(response);
    }

    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> Archive(Guid projectId, Guid cardId, Guid commentId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new ArchiveCommentCommand(projectId, cardId, commentId, userId);
        var result = await commentService.ArchiveAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new CommentResponse(
            result.Value.Id,
            result.Value.CardId,
            result.Value.AuthorId,
            result.Value.AuthorUsername,
            result.Value.Content,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt,
            result.Value.MentionedUserIds
        );
        return Ok(response);
    }
}

