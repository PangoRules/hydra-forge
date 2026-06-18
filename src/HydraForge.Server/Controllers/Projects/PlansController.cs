using HydraForge.Application.Plans;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPlans = HydraForge.Application.Plans;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/cards/{cardId:guid}/plans")]
public class PlansController(PlanService planService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid cardId, [FromBody] AppPlans.CreatePlanRequest request)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppPlans.CreatePlanCommand(
            projectId,
            cardId,
            null,
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
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt
        );

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, planId = result.Value.Id },
            response
        );
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();

        var result = await planService.ListByCardAsync(projectId, cardId, new AppPlans.PlanListFilter(), userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppPlans.PlanListResponse(
            result.Value.Select(p => new AppPlans.PlanResponse(
                p.Id,
                p.ProjectId,
                p.CardId,
                p.Title,
                p.Description,
                p.Content,
                p.Version,
                p.CreatedByUserId,
                p.CreatedAt,
                p.UpdatedAt
            )).ToList()
        );

        return Ok(response);
    }

    [HttpGet("~/api/projects/{projectId:guid}/plans/{planId:guid}")]
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
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt
        );

        return Ok(response);
    }

    [HttpPut("~/api/projects/{projectId:guid}/plans/{planId:guid}")]
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
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt
        );

        return Ok(response);
    }

    [HttpGet("~/api/projects/{projectId:guid}/plans/{planId:guid}/versions")]
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

    [HttpPost("~/api/projects/{projectId:guid}/plans/{planId:guid}/restore")]
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
            result.Value.CardId,
            result.Value.Title,
            result.Value.Description,
            result.Value.Content,
            result.Value.Version,
            result.Value.CreatedByUserId,
            result.Value.CreatedAt,
            result.Value.UpdatedAt
        );

        return Ok(response);
    }
}