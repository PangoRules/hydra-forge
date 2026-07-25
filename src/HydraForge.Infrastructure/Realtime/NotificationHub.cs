using HydraForge.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

public interface INotificationHub
{
    Task OnNotificationReceived(object notification);
}

[Authorize]
public class NotificationHub : Hub<INotificationHub>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetRequiredUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }
}
