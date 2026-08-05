using HydraForge.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

public sealed class SignalRChatBroadcaster(IHubContext<ChatHub, IChatHub> hubContext)
    : IChatBroadcaster
{
    public IChatHub Group(Guid sessionId) =>
        hubContext.Clients.Group(ChatHub.SessionGroup(sessionId));
}
