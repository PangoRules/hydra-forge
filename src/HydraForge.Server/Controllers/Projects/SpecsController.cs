using HydraForge.Application.Auth;
using HydraForge.Application.Shared;
using HydraForge.Application.Specs;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using HydraForge.Server.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/[controller]")]
public class SpecsController(
    SpecService specService,
    IHtmlToMarkdownConverter htmlToMarkdown,
    IMarkdownToHtmlConverter markdownToHtml
) : ControllerBase
{
    // Content is stored as Markdown. A client that authors in HTML (the Web UI's
    // TipTap editor) sends/expects HTML on both write and read — the same
    // X-Content-Format header drives conversion in both directions so Content
    // never leaves this boundary in the wrong shape for whichever client asked.
    private string InContent(string content) =>
        ContentFormatHeader.IsHtml(Request) ? htmlToMarkdown.Convert(content) : content;

    private string OutContent(string content) =>
        ContentFormatHeader.IsHtml(Request) ? markdownToHtml.Convert(content) : content;

    [HttpPost("cards/{cardId:guid}")]
    [ProducesResponseType(typeof(SpecResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        Guid projectId,
        Guid cardId,
        [FromBody] CreateSpecRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new CreateSpecCommand(
            projectId,
            cardId,
            userId,
            request.DocType,
            request.Title,
            request.Description,
            InContent(request.Content)
        );

        var result = await specService.CreateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new SpecResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.DocType,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt
        );

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, specId = result.Value.Id },
            response
        );
    }

    [HttpGet("cards/{cardId:guid}")]
    [ProducesResponseType(typeof(SpecListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();

        var result = await specService.ListByCardAsync(
            projectId,
            cardId,
            new SpecListFilter(),
            userId
        );

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new SpecListResponse([
            .. result.Value.Select(s => new SpecResponse(
                s.Id,
                s.ProjectId,
                s.CardId,
                s.DocType,
                s.Title,
                s.Description,
                OutContent(s.Content),
                s.Version,
                s.CreatedByUserId,
                s.CreatedAt,
                s.UpdatedAt
            )),
        ]);

        return Ok(response);
    }

    [HttpGet("{specId:guid}")]
    [ProducesResponseType(typeof(SpecResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid projectId, Guid specId)
    {
        var userId = User.GetRequiredUserId();

        var result = await specService.GetByIdAsync(projectId, specId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new SpecResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.DocType,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt
        );

        return Ok(response);
    }

    [HttpPut("{specId:guid}")]
    [ProducesResponseType(typeof(SpecResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid specId,
        [FromBody] UpdateSpecRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new UpdateSpecCommand(
            projectId,
            specId,
            userId,
            request.Title,
            request.Description,
            InContent(request.Content)
        );

        var result = await specService.UpdateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new SpecResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.DocType,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt
        );

        return Ok(response);
    }

    [HttpGet("{specId:guid}/versions")]
    [ProducesResponseType(typeof(SpecVersionListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListVersions(Guid projectId, Guid specId)
    {
        var userId = User.GetRequiredUserId();

        var result = await specService.ListVersionsAsync(projectId, specId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new SpecVersionListResponse([
            .. result.Value.Select(v => new SpecVersionResponse(
                v.Id,
                v.SpecId,
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

    [HttpPost("{specId:guid}/restore")]
    [ProducesResponseType(typeof(SpecResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(
        Guid projectId,
        Guid specId,
        [FromBody] RestoreSpecVersionRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new RestoreSpecVersionCommand(projectId, specId, request.Version, userId);

        var result = await specService.RestoreVersionAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new SpecResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.DocType,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt
        );

        return Ok(response);
    }
}
