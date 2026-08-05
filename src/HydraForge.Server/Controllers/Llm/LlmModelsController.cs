using HydraForge.Application.Auth;
using HydraForge.Application.Llm;
using HydraForge.Domain.Enums;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Llm;

/// <summary>
/// Read-only, non-admin surface — models a regular user may choose between for a given
/// AI feature. Distinct from <c>LlmAdminController</c>, which is admin-only CRUD.
/// </summary>
[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/llm/models")]
public class LlmModelsController(IModelRouter modelRouter) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AvailableModelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List([FromQuery] AiFeature feature)
    {
        var result = await modelRouter.ListAvailableModelsAsync(feature);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }
}
