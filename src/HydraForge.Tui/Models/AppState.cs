using HydraForge.Tui.Screens;
using System.Threading;

namespace HydraForge.Tui.Models;

public enum ConnectionStatus
{
    Connected,
    Reconnecting,
    Disconnected,
}

public class AppState
{
    public IScreen? CurrentScreen { get; set; }
    public IScreen? PreviousScreen { get; set; }
    public Guid? SelectedProjectId { get; set; }
    public Guid? SelectedCardId { get; set; }
    public ConnectionStatus Connection { get; set; } = ConnectionStatus.Disconnected;
    public List<(DateTime Timestamp, string CorrelationId, string Message)> Errors { get; } = [];
    public int OnlineCount { get; set; }

    private int _unreadNotifications;
    public int UnreadNotifications
    {
        get => Volatile.Read(ref _unreadNotifications);
        set => Volatile.Write(ref _unreadNotifications, value);
    }

    public int IncrementUnreadNotifications() => Interlocked.Increment(ref _unreadNotifications);

    public int? BoardCursorCol { get; set; }
    public int? BoardCursorCard { get; set; }
}
