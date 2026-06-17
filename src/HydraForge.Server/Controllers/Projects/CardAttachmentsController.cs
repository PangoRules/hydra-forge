using HydraForge.Application.Attachments;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/cards/{cardId:guid}/attachments")]
public class CardAttachmentsController(AttachmentService attachmentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();
        var result = await attachmentService.ListAsync(projectId, cardId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new AttachmentListResponse([
            .. result.Value.Select(a => new AttachmentResponse(
                a.Id,
                a.CardId,
                a.FileName,
                a.ContentType,
                a.Size,
                a.CreatedAt
            )),
        ]);
        return Ok(response);
    }

    [HttpPost]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> Create(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();

        if (!Request.HasFormContentType)
            return BadRequest(new { error = "Expected multipart/form-data" });

        var form = await Request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();
        if (file == null)
            return BadRequest(new { error = "No file provided" });

        await using var stream = file.OpenReadStream();
        var cmd = new CreateAttachmentCommand(
            projectId,
            cardId,
            userId,
            file.FileName,
            file.ContentType,
            file.Length,
            stream
        );

        var result = await attachmentService.CreateAsync(cmd);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var response = new AttachmentResponse(
            result.Value.Id,
            result.Value.CardId,
            result.Value.FileName,
            result.Value.ContentType,
            result.Value.Size,
            result.Value.CreatedAt
        );
        return CreatedAtAction(nameof(List), new { projectId, cardId }, response);
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid projectId, Guid cardId, Guid attachmentId)
    {
        var userId = User.GetRequiredUserId();
        var result = await attachmentService.DownloadAsync(projectId, cardId, attachmentId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        var (stream, contentType, fileName) = result.Value;
        return File(stream, contentType, fileName);
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid cardId, Guid attachmentId)
    {
        var userId = User.GetRequiredUserId();
        var result = await attachmentService.DeleteAsync(projectId, cardId, attachmentId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }
}

public record AttachmentResponse(
    Guid Id,
    Guid CardId,
    string FileName,
    string ContentType,
    long Size,
    DateTime CreatedAt
);

public record AttachmentListResponse(IReadOnlyList<AttachmentResponse> Attachments);
