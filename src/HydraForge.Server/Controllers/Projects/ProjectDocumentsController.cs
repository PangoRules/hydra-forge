using HydraForge.Application.Auth;
using HydraForge.Application.ProjectDocuments;
using HydraForge.Application.Shared;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using HydraForge.Server.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/[controller]")]
public class ProjectDocumentsController(
    ProjectDocumentService docService,
    IHtmlToMarkdownConverter htmlToMarkdown,
    IMarkdownToHtmlConverter markdownToHtml,
    ILogger<ProjectDocumentsController> logger
) : ControllerBase
{
    private readonly ProjectDocumentService _docService = docService;
    private readonly ILogger<ProjectDocumentsController> _logger = logger;

    private string InContent(string content) =>
        ContentFormatHeader.IsHtml(Request) ? htmlToMarkdown.Convert(content) : content;

    private string OutContent(string content) =>
        ContentFormatHeader.IsHtml(Request) ? markdownToHtml.Convert(content) : content;

    [HttpPost]
    [ProducesResponseType(typeof(ProjectDocumentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateProjectDocumentRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new CreateProjectDocumentCommand(
            projectId,
            request.DocType,
            request.Title,
            request.Description,
            InContent(request.Content),
            userId
        );

        var result = await _docService.CreateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectDocumentResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.DocType,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt
        );

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, documentId = result.Value.Id },
            response
        );
    }

    [HttpGet]
    [ProducesResponseType(typeof(ProjectDocumentListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid projectId)
    {
        var userId = User.GetRequiredUserId();

        var result = await _docService.ListAsync(projectId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectDocumentListResponse([
            .. result.Value.Select(d => new ProjectDocumentResponse(
                d.Id,
                d.ProjectId,
                d.DocType,
                d.Title,
                d.Description,
                OutContent(d.Content),
                d.Version,
                d.CreatedByUserId,
                d.CreatedAt,
                d.UpdatedAt,
                d.ArchivedAt
            )),
        ]);

        return Ok(response);
    }

    [HttpGet("{documentId:guid}")]
    [ProducesResponseType(typeof(ProjectDocumentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid projectId, Guid documentId)
    {
        var userId = User.GetRequiredUserId();

        var result = await _docService.GetByIdAsync(projectId, documentId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectDocumentResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.DocType,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt
        );

        return Ok(response);
    }

    [HttpPut("{documentId:guid}")]
    [ProducesResponseType(typeof(ProjectDocumentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid documentId,
        [FromBody] UpdateProjectDocumentRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new UpdateProjectDocumentCommand(
            projectId,
            documentId,
            request.Title,
            request.Description,
            InContent(request.Content),
            userId
        );

        var result = await _docService.UpdateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectDocumentResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.DocType,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt
        );

        return Ok(response);
    }

    [HttpGet("{documentId:guid}/versions")]
    [ProducesResponseType(typeof(ProjectDocumentVersionListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListVersions(Guid projectId, Guid documentId)
    {
        var userId = User.GetRequiredUserId();

        var result = await _docService.ListVersionsAsync(projectId, documentId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectDocumentVersionListResponse([
            .. result.Value.Select(v => new ProjectDocumentVersionResponse(
                v.Id,
                v.ProjectDocumentId,
                v.Version,
                v.Title,
                v.Description,
                OutContent(v.Content),
                v.CreatedAt,
                v.CreatedByUserId
            )),
        ]);

        return Ok(response);
    }

    [HttpPost("{documentId:guid}/restore")]
    [ProducesResponseType(typeof(ProjectDocumentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(
        Guid projectId,
        Guid documentId,
        [FromBody] RestoreProjectDocumentVersionRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new RestoreProjectDocumentVersionCommand(
            projectId,
            documentId,
            request.Version,
            userId
        );

        var result = await _docService.RestoreVersionAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectDocumentResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.DocType,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.ArchivedAt
        );

        return Ok(response);
    }
}
