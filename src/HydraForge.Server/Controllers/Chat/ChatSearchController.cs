using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Chat;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/chat/search")]
public class ChatSearchController(IChatSearchService searchService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChatSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] Guid? projectId = null,
        [FromQuery] ChatSessionScope scope = ChatSessionScope.Mine
    )
    {
        var userId = User.GetRequiredUserId();
        var results = await searchService.SearchAsync(userId, q, projectId, scope);
        return Ok(results);
    }
}
