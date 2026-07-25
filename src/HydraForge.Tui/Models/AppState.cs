using HydraForge.Tui.Screens;

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
    public int UnreadNotifications { get; set; }
    public int? BoardCursorCol { get; set; }
    public int? BoardCursorCard { get; set; }
}
