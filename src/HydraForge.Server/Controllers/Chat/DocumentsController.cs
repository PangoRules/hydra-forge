using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Domain.Common;
using HydraForge.Domain.Constants;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Chat;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/[controller]")]
public class DocumentsController(
    IDocumentService documentService,
    IDocumentIngestionService ingestionService
) : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "text/plain",
        "text/markdown",
        "text/code",
        "text/csv",
        "text/html",
        "application/pdf",
    ];

    [HttpPost]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        [FromForm] string? title,
        [FromForm] string? content,
        [FromForm] string? contentType
    )
    {
        var userId = User.GetRequiredUserId();

        bool hasFile = file is not null;
        bool hasText = !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(content);

        if (hasFile && hasText)
        {
            return this.ToProblemResult(
                new Domain.Common.Error(
                    DomainErrorCodes.Validation.InvalidValue,
                    "Provide either a file or text fields, not both."
                )
            );
        }

        if (!hasFile && !hasText)
        {
            return this.ToProblemResult(
                new Domain.Common.Error(
                    DomainErrorCodes.Validation.Required,
                    "A file or text fields (title + content) are required."
                )
            );
        }

        if (hasFile)
        {
            var fileContentType = file!.ContentType;

            if (!AllowedContentTypes.Contains(fileContentType))
            {
                return this.ToProblemResult(
                    new Domain.Common.Error(
                        DomainErrorCodes.Attachments.UnsupportedContentType,
                        $"Content type '{fileContentType}' is not supported for document upload."
                    )
                );
            }

            var fileTitle = Path.GetFileNameWithoutExtension(file.FileName);
            await using var stream = file.OpenReadStream();
            var result = await ingestionService.IngestAsync(
                userId,
                fileTitle,
                string.Empty,
                fileContentType,
                stream
            );

            if (result.IsFailure)
                return this.ToProblemResult(result.Error);

            var doc = result.Value;
            return CreatedAtAction(nameof(GetById), new { documentId = doc.Id }, MapToDto(doc));
        }

        // text path
        var ingestResult = await ingestionService.IngestAsync(
            userId,
            title!,
            content!,
            contentType ?? "text/plain"
        );

        if (ingestResult.IsFailure)
            return this.ToProblemResult(ingestResult.Error);

        var created = ingestResult.Value;
        return CreatedAtAction(nameof(GetById), new { documentId = created.Id }, MapToDto(created));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? q = null)
    {
        var userId = User.GetRequiredUserId();
        var result = await documentService.ListAsync(userId, q);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpGet("{documentId:guid}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid documentId)
    {
        var userId = User.GetRequiredUserId();
        var result = await documentService.GetByIdAsync(documentId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid documentId)
    {
        var userId = User.GetRequiredUserId();
        var result = await documentService.ArchiveAsync(documentId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return NoContent();
    }

    private static DocumentDto MapToDto(Domain.Entities.PersonalSpace.Document doc) =>
        new(
            doc.Id,
            doc.Title,
            doc.ContentType,
            doc.Language,
            doc.Version,
            doc.CreatedAt,
            doc.UpdatedAt,
            doc.ArchivedAt
        );
}
