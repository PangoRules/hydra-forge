namespace HydraForge.Server.Controllers;

using HydraForge.Application.Auth;
using HydraForge.Application.Llm;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/account")]
public class AccountController(ILlmAdminService llmAdmin) : ControllerBase
{
    [HttpGet("usage")]
    [ProducesResponseType(typeof(AccountUsageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsage(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var result = await llmAdmin.GetAccountUsageAsync(userId, ct);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }
}
