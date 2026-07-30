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

    // Presence — userId keyed so joins/leaves/reconnects are idempotent. Populated by
    // whichever screen currently owns the PresenceHub subscription (BoardScreen,
    // CardDetailScreen); read by BoardRenderer's status bar and CardDetailScreen's
    // "N viewing" line.
    public Dictionary<Guid, string> OnlineUsers { get; } = [];
    public Dictionary<Guid, Guid> FocusedCards { get; } = [];

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
