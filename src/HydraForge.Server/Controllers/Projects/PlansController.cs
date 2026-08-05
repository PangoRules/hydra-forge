using HydraForge.Application.Auth;
using HydraForge.Application.Plans;
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
public class PlansController(
    PlanService planService,
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
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        Guid projectId,
        Guid cardId,
        [FromBody] CreatePlanRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new CreatePlanCommand(
            projectId,
            cardId,
            request.SpecId,
            userId,
            request.Title,
            request.Description,
            InContent(request.Content),
            request.Position
        );

        var result = await planService.CreateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new PlanResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.SpecId,
            result.Value.Status,
            result.Value.Position
        );

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, planId = result.Value.Id },
            response
        );
    }

    [HttpGet("cards/{cardId:guid}")]
    [ProducesResponseType(typeof(PlanListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();

        var result = await planService.ListByCardAsync(
            projectId,
            cardId,
            new PlanListFilter(),
            userId
        );

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new PlanListResponse([
            .. result.Value.Select(p => new PlanResponse(
                p.Id,
                p.ProjectId,
                p.CardId,
                p.Title,
                p.Description,
                OutContent(p.Content),
                p.Version,
                p.CreatedByUserId,
                p.CreatedAt,
                p.UpdatedAt,
                p.SpecId,
                p.Status,
                p.Position
            )),
        ]);

        return Ok(response);
    }

    [HttpGet("{planId:guid}")]
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid projectId, Guid planId)
    {
        var userId = User.GetRequiredUserId();

        var result = await planService.GetByIdAsync(projectId, planId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new PlanResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.SpecId,
            result.Value.Status,
            result.Value.Position
        );

        return Ok(response);
    }

    [HttpPut("{planId:guid}")]
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid planId,
        [FromBody] UpdatePlanRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new UpdatePlanCommand(
            projectId,
            planId,
            userId,
            request.Title,
            request.Description,
            InContent(request.Content)
        );

        var result = await planService.UpdateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new PlanResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.SpecId,
            result.Value.Status,
            result.Value.Position
        );

        return Ok(response);
    }

    [HttpGet("{planId:guid}/versions")]
    [ProducesResponseType(typeof(PlanVersionListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListVersions(Guid projectId, Guid planId)
    {
        var userId = User.GetRequiredUserId();

        var result = await planService.ListVersionsAsync(projectId, planId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new PlanVersionListResponse([
            .. result.Value.Select(v => new PlanVersionResponse(
                v.Id,
                v.PlanId,
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

    [HttpPost("{planId:guid}/restore")]
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(
        Guid projectId,
        Guid planId,
        [FromBody] RestorePlanVersionRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new RestorePlanVersionCommand(projectId, planId, request.Version, userId);

        var result = await planService.RestoreVersionAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new PlanResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.SpecId,
            result.Value.Status,
            result.Value.Position
        );

        return Ok(response);
    }

    [HttpPatch("{planId:guid}/status")]
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(
        Guid projectId,
        Guid planId,
        [FromBody] SetPlanStatusRequest request,
        CancellationToken ct
    )
    {
        var actorId = User.GetRequiredUserId();
        var result = await planService.SetStatusAsync(
            new SetPlanStatusCommand(projectId, planId, actorId, request.Status),
            ct
        );
        if (!result.IsSuccess)
            return this.ToProblemResult(result.Error);
        var response = new PlanResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            OutContent(result.Value.Content),
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.SpecId,
            result.Value.Status,
            result.Value.Position
        );
        return Ok(response);
    }
}
