using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly IProjectMemberRepository _memberRepo;

    public UsersController(IUserRepository userRepo, IProjectMemberRepository memberRepo)
    {
        _userRepo = userRepo;
        _memberRepo = memberRepo;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 10,
        [FromQuery] Guid? excludeProjectId = null)
    {
        // Load existing member IDs if excluding a project
        HashSet<Guid>? excludeIds = null;
        if (excludeProjectId.HasValue)
        {
            var members = await _memberRepo.ListMembersAsync(excludeProjectId.Value);
            excludeIds = members.Select(m => m.UserId).ToHashSet();
        }

        var users = (await _userRepo.FindByUsernamesAsync(
            [],
            string.IsNullOrWhiteSpace(q) ? null : q,
            limit
        )).Values.Where(u => excludeIds == null || !excludeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToList();

        return Ok(users);
    }
}