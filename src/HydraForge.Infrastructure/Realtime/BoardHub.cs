using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

public interface IBoardHub
{
    Task OnBoardEvent(ProjectBoardEventEnvelope envelope);
}

[Authorize]
public class BoardHub : Hub<IBoardHub>
{
    public static string ProjectGroup(Guid projectId) => $"project-{projectId}";

    private readonly IProjectMemberRepository _memberRepo;

    public BoardHub(IProjectMemberRepository memberRepo)
    {
        _memberRepo = memberRepo;
    }

    public async Task JoinProject(Guid projectId)
    {
        var userId = Context.User.GetRequiredUserId();

        var isAdmin = Context.User.IsInRole("Admin");
        if (!isAdmin)
        {
            var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, userId);
            if (membership == null)
            {
                throw new HubException("Access denied");
            }
        }

        var groupName = ProjectGroup(projectId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}
