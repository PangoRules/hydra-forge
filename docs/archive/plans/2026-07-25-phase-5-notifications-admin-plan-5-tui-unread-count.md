# Plan 5: TUI Unread Count in Status Bar

**Branch:** `task/tui-unread-count`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 5

## Task

Wire `AppState.UnreadNotifications` into `BoardRenderer` status bar. Add polling for unread count via API. Connect to `NotificationHub` SignalR for real-time updates. Add `U` key to open unread notifications list.

## Files to modify

- `src/HydraForge.Tui/Renderers/BoardRenderer.cs` — add `unreadCount` parameter to `BuildLayout()`, display in status bar
- `src/HydraForge.Tui/Screens/BoardScreen.cs` — fetch unread count, pass to renderer, add `U` key handler, connect NotificationHub
- `src/HydraForge.Tui/Services/SignalRConnectionManager.cs` — add NotificationHub connection

## Implementation steps

### Step 1: Update BoardRenderer status bar

In `src/HydraForge.Tui/Renderers/BoardRenderer.cs`, add `int unreadCount = 0` parameter to `BuildLayout()`:

```csharp
public Layout BuildLayout(
    List<ColumnData> columns,
    int selectedColumn,
    int selectedCard,
    string projectName,
    int totalCards,
    ConnectionStatus connection = ConnectionStatus.Disconnected,
    int onlineCount = 0,
    int errorCount = 0,
    Guid? reorderCardId = null,
    int unreadCount = 0)
```

Update the status bar line (around line 155):

```csharp
layout["Status"].Update(
    new Panel(
        new Markup($"[{dotColor}]●[/] [grey]{statusText}    |    {onlineCount} online    |    {unreadCount} unread    |    {errorCount} errors[/]")
    ).Expand()
);
```

### Step 2: Add unread count field and fetch to BoardScreen

In `src/HydraForge.Tui/Screens/BoardScreen.cs`, add field:

```csharp
private int _unreadCount;
```

Add method to fetch unread count:

```csharp
private async Task FetchUnreadCountAsync()
{
    try
    {
        var response = await Client.NotificationsUnreadCountAsync();
        _unreadCount = response.Count;
        _appState.UnreadNotifications = _unreadCount;
    }
    catch
    {
        // Non-fatal — status bar shows last known count
    }
}
```

Update `RenderAsync()` to pass `_unreadCount`:

```csharp
var layout = _renderer.BuildLayout(
    _columns,
    _selectedColumn,
    _selectedCard,
    _projectName,
    totalCards,
    _appState.Connection,
    _appState.OnlineCount,
    _errorCollector.Count,
    _reorderCardId,
    _unreadCount
);
```

Call `FetchUnreadCountAsync()` in `OnEnterAsync()` after loading board:

```csharp
await LoadBoardAsync();
await FetchUnreadCountAsync();
```

### Step 3: Add NotificationHub connection to SignalRConnectionManager

In `src/HydraForge.Tui/Services/SignalRConnectionManager.cs`, add:

```csharp
private HubConnection? _notificationConnection;

public event Action<int>? OnUnreadCountChanged;
```

In `ConnectAsync`, after the presence connection setup, add:

```csharp
// NotificationHub connection — stays connected for app lifetime
_notificationConnection = new HubConnectionBuilder()
    .WithUrl($"{serverUrl}/hubs/notifications", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(token)!;
    })
    .WithAutomaticReconnect()
    .Build();

_notificationConnection.On<JsonElement>("NotificationReceived", _ =>
{
    _appState.UnreadNotifications++;
    OnUnreadCountChanged?.Invoke(_appState.UnreadNotifications);
});

await _notificationConnection.StartAsync();
```

In `DisconnectAsync`, add cleanup:

```csharp
if (_notificationConnection != null)
{
    await _notificationConnection.StopAsync();
    await _notificationConnection.DisposeAsync();
}
```

### Step 4: Wire NotificationHub events in BoardScreen

In `src/HydraForge.Tui/Screens/BoardScreen.cs`, in `OnEnterAsync()`, after creating `_signalR`, subscribe to unread count changes:

```csharp
_signalR.OnUnreadCountChanged += async count =>
{
    _unreadCount = count;
    await RenderAsync();
};
```

### Step 5: Add `U` key handler for notifications list

In `BoardScreen.HandleKeyAsync`, add case for `U` key:

```csharp
case ConsoleKey.U:
    await ShowNotificationsAsync();
    break;
```

Add method:

```csharp
private async Task ShowNotificationsAsync()
{
    try
    {
        var notifications = await Client.NotificationsGETAsync(skip: 0, take: 50);
        if (notifications == null || notifications.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No notifications.[/]");
            Console.ReadKey(true);
            await RenderAsync();
            return;
        }

        var choices = notifications.Select(n =>
        {
            var prefix = n.IsRead ? "  " : "● ";
            var time = n.CreatedAt.ToString("MMM dd HH:mm");
            return $"{prefix}[bold]{Markup.Escape(n.Title)}[/] [grey]{time}[/]";
        }).ToList();

        var selected = await ListPrompt.Show("Notifications", choices, renderBackdrop: RenderAsync);
        if (selected != null)
        {
            var idx = choices.IndexOf(selected);
            if (idx >= 0 && idx < notifications.Count)
            {
                var notif = notifications[idx];
                if (!notif.IsRead)
                {
                    await Client.NotificationsPUTAsync(notif.Id);
                    _unreadCount = Math.Max(0, _unreadCount - 1);
                    _appState.UnreadNotifications = _unreadCount;
                }
            }
        }
        await RenderAsync();
    }
    catch (Exception ex)
    {
        _errorCollector.Add("N/A", $"Notifications error: {ex.Message}");
        await RenderAsync();
    }
}
```

### Step 6: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Tui.Tests/
```

## Verification

- `dotnet build` — no errors
- `dotnet test` — all existing tests still pass
- Manual: start TUI, open board, status bar shows "0 unread". Trigger notification (via Task 7), see count increment. Press `U` to view list.

## Dependencies

- Task 2 (Notification API endpoints must exist)
- Task 3 (NotificationHub must exist for real-time updates)
- Task 4 (API routes defined — TUI uses same `HydraForgeApiClient` generated from OpenAPI)