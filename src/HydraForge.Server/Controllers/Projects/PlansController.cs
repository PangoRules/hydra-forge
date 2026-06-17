using HydraForge.Application.Plans;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPlans = HydraForge.Application.Plans;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/plans")]
public class PlansController(PlanService planService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] AppPlans.CreatePlanRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppPlans.CreatePlanCommand(
            projectId,
            userId,
            request.Title,
            request.Description,
            request.Content
        );

        var result = await planService.CreateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppPlans.PlanResponse(
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
            new { projectId, planId = result.Value.Id },
            response
        );
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId)
    {
        var userId = User.GetRequiredUserId();

        var result = await planService.ListAsync(projectId, new AppPlans.PlanListFilter(), userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppPlans.PlanListResponse(
            result.Value.Select(p => new AppPlans.PlanResponse(
                p.Id,
                p.ProjectId,
                p.Title,
                p.Description,
                p.Content,
                p.Version,
                p.CreatedByUserId,
                p.CreatedAt,
                p.UpdatedAt,
                p.LinkedCardId
            )).ToList()
        );

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

        var response = new AppPlans.PlanResponse(
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

    [HttpPut("{planId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, [FromBody] AppPlans.UpdatePlanRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppPlans.UpdatePlanCommand(
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

        var response = new AppPlans.PlanResponse(
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

    [HttpGet("{planId:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid projectId, Guid planId)
    {
        var userId = User.GetRequiredUserId();

        var result = await planService.ListVersionsAsync(projectId, planId, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppPlans.PlanVersionListResponse(
            result.Value.Select(v => new AppPlans.PlanVersionResponse(
                v.Id,
                v.PlanId,
                v.Version,
                v.Content,
                v.CreatedAt,
                v.CreatedByUserId
            )).ToList()
        );

        return Ok(response);
    }

    [HttpPost("{planId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid projectId, Guid planId, [FromBody] AppPlans.RestorePlanVersionRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppPlans.RestorePlanVersionCommand(
            projectId,
            planId,
            request.Version,
            userId
        );

        var result = await planService.RestoreVersionAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppPlans.PlanResponse(
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

    [HttpPost("{planId:guid}/link")]
    public async Task<IActionResult> LinkToCard(Guid projectId, Guid planId, [FromBody] AppPlans.LinkPlanToCardRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppPlans.LinkPlanToCardCommand(
            projectId,
            planId,
            request.CardId,
            userId
        );

        var result = await planService.LinkToCardAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("{planId:guid}/link")]
    public async Task<IActionResult> UnlinkFromCard(Guid projectId, Guid planId, [FromQuery] Guid cardId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppPlans.UnlinkPlanFromCardCommand(
            projectId,
            cardId,
            userId
        );

        var result = await planService.UnlinkFromCardAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }
}
