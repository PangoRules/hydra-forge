using System.Security.Claims;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Domain.Constants;

namespace HydraForge.Server.Auth;

public static class ClaimsPrincipalExtensions
{
    public static async Task<bool> IsProjectMemberOrAdmin(
        this ClaimsPrincipal user,
        IProjectMemberRepository memberRepo,
        Guid projectId,
        CancellationToken ct = default
    )
    {
        if (user.IsInRole(Roles.Admin))
            return true;

        var userId = user.GetRequiredUserId();
        var membership = await memberRepo.GetByProjectAndUserAsync(projectId, userId, ct);
        return membership != null;
    }
}
