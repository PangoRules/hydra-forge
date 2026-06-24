using HydraForge.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

public class SignalRProjectBoardEventPublisher : IProjectBoardEventPublisher
{
    private readonly IHubContext<BoardHub, IBoardHub> _boardHubContext;

    public SignalRProjectBoardEventPublisher(
        IHubContext<BoardHub, IBoardHub> boardHubContext)
    {
        _boardHubContext = boardHubContext;
    }

    public async Task PublishAsync(ProjectBoardEventEnvelope envelope, CancellationToken ct = default)
    {
        var groupName = BoardHub.ProjectGroup(envelope.ProjectId);
        await _boardHubContext.Clients.Group(groupName).OnBoardEvent(envelope);
    }
}
