using HydraForge.Application.Specs;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppSpecs = HydraForge.Application.Specs;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/specs")]
public class SpecsController(SpecService specService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] AppSpecs.CreateSpecRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppSpecs.CreateSpecCommand(
            projectId,
            userId,
            request.Title,
            request.Description,
            request.Content
        );

        var result = await specService.CreateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppSpecs.SpecResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.LinkedCardId
        );

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, specId = result.Value.Id },
            response
        );
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId)
    {
        var userId = User.GetRequiredUserId();

        var result = await specService.ListAsync(projectId, new AppSpecs.SpecListFilter(), userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppSpecs.SpecListResponse(
            result.Value.Select(s => new AppSpecs.SpecResponse(
                s.Id,
                s.ProjectId,
                s.Title,
                s.Description,
                s.Content,
                s.Version,
                s.CreatedByUserId,
                s.CreatedAt,
                s.UpdatedAt,
                s.LinkedCardId
            )).ToList()
        );

        return Ok(response);
    }

    [HttpGet("{specId:guid}")]
    public async Task<IActionResult> GetById(Guid projectId, Guid specId)
    {
        var userId = User.GetRequiredUserId();

        var result = await specService.GetByIdAsync(projectId, specId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppSpecs.SpecResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.LinkedCardId
        );

        return Ok(response);
    }

    [HttpPut("{specId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid specId, [FromBody] AppSpecs.UpdateSpecRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppSpecs.UpdateSpecCommand(
            projectId,
            specId,
            userId,
            request.Title,
            request.Description,
            request.Content
        );

        var result = await specService.UpdateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppSpecs.SpecResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.LinkedCardId
        );

        return Ok(response);
    }

    [HttpGet("{specId:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid projectId, Guid specId)
    {
        var userId = User.GetRequiredUserId();

        var result = await specService.ListVersionsAsync(projectId, specId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppSpecs.SpecVersionListResponse(
            result.Value.Select(v => new AppSpecs.SpecVersionResponse(
                v.Id,
                v.SpecId,
                v.Version,
                v.Content,
                v.CreatedAt,
                v.CreatedByUserId
            )).ToList()
        );

        return Ok(response);
    }

    [HttpPost("{specId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid projectId, Guid specId, [FromBody] AppSpecs.RestoreSpecVersionRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppSpecs.RestoreSpecVersionCommand(
            projectId,
            specId,
            request.Version,
            userId
        );

        var result = await specService.RestoreVersionAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppSpecs.SpecResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt,
            result.Value.LinkedCardId
        );

        return Ok(response);
    }

    [HttpPost("{specId:guid}/link")]
    public async Task<IActionResult> LinkToCard(Guid projectId, Guid specId, [FromBody] AppSpecs.LinkSpecToCardRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppSpecs.LinkSpecToCardCommand(
            projectId,
            specId,
            request.CardId,
            userId
        );

        var result = await specService.LinkToCardAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("{specId:guid}/link")]
    public async Task<IActionResult> UnlinkFromCard(Guid projectId, Guid specId, [FromQuery] Guid cardId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppSpecs.UnlinkSpecFromCardCommand(
            projectId,
            cardId,
            userId
        );

        var result = await specService.UnlinkFromCardAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }
}
