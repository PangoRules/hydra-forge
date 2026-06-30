using HydraForge.Application.Plans;
using HydraForge.Application.Auth;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/[controller]")]
public class PlansController(PlanService planService) : ControllerBase
{
    [HttpPost("cards/{cardId:guid}")]
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
            request.Content,
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
            result.Value.Content,
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
                p.Content,
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
            result.Value.Content,
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
            request.Content
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
            result.Value.Content,
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
                v.Content,
                v.CreatedAt,
                v.CreatedByUserId
            )),
        ]);

        return Ok(response);
    }

    [HttpPost("{planId:guid}/restore")]
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
            result.Value.Content,
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

    [HttpPost("{planId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid projectId, Guid planId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await planService.ActivateAsync(new ActivatePlanCommand(projectId, planId, actorId), ct);
        if (!result.IsSuccess)
            return this.ToProblemResult(result.Error);
        var response = new PlanResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
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

    [HttpPost("{planId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid projectId, Guid planId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await planService.CompleteAsync(new CompletePlanCommand(projectId, planId, actorId), ct);
        if (!result.IsSuccess)
            return this.ToProblemResult(result.Error);
        var response = new PlanResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
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

    [HttpPost("{planId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid projectId, Guid planId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await planService.ReactivateAsync(new ReactivatePlanCommand(projectId, planId, actorId), ct);
        if (!result.IsSuccess)
            return this.ToProblemResult(result.Error);
        var response = new PlanResponse(
            result.Value.Id,
            result.Value.ProjectId,
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
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
