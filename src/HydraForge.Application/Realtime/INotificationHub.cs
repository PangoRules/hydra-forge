namespace HydraForge.Application.Realtime;

public interface INotificationHub
{
    Task OnNotificationReceived(NotificationReceivedEvent notification);
}
