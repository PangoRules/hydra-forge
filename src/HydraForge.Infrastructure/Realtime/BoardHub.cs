using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

public interface IBoardHub
{
    Task OnBoardEvent(ProjectBoardEventEnvelope envelope);
}

[Authorize]
[EnableRateLimiting("SignalR")]
public class BoardHub(IProjectMemberRepository memberRepo) : Hub<IBoardHub>
{
    public static string ProjectGroup(Guid projectId) => $"project-{projectId}";

    private readonly IProjectMemberRepository _memberRepo = memberRepo;

    public async Task JoinProject(Guid projectId)
    {
        var userId = Context.User!.GetRequiredUserId();

        var isAdmin = Context.User!.IsInRole(Roles.Admin);
        if (!isAdmin)
        {
            _ =
                await _memberRepo.GetByProjectAndUserAsync(projectId, userId)
                ?? throw new HubException("Access denied");
        }

        var groupName = ProjectGroup(projectId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}
