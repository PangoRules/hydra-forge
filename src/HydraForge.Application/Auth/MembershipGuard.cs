using HydraForge.Application.Projects;

namespace HydraForge.Application.Auth;

public static class MembershipGuard
{
    public static async Task<bool> HasAccessAsync(
        IUserRepository userRepo,
        IProjectMemberRepository memberRepo,
        Guid projectId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        if (await userRepo.IsAdminAsync(userId, ct))
            return true;

        var membership = await memberRepo.GetByProjectAndUserAsync(projectId, userId, ct);
        return membership != null;
    }
}
