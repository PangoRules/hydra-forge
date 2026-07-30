using HydraForge.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

public class SignalRProjectBoardEventPublisher(IHubContext<BoardHub, IBoardHub> boardHubContext)
    : IProjectBoardEventPublisher
{
    public async Task PublishAsync(
        ProjectBoardEventEnvelope envelope,
        CancellationToken ct = default
    )
    {
        var groupName = BoardHub.ProjectGroup(envelope.ProjectId);
        await boardHubContext.Clients.Group(groupName).OnBoardEvent(envelope);
    }
}
