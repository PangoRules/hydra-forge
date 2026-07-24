# Plan 17: Status Bar — Sync, Notifications, Presence, Error Panel

**Branch:** `task/tui-status-bar`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 17

**Goal:** Persistent status bar at bottom of board view showing connection status, online presence, unread notifications, error count. Error panel expandable with `E` key.

**Depends on:** Task 5 (ConnectionManager), Task 8 (SignalR for presence/online count).

---

## Step 1: Create `StatusBarRenderer`

Create `src/HydraForge.Tui/Renderers/StatusBarRenderer.cs`:

```csharp
using HydraForge.Tui.Models;
using Spectre.Console;

namespace HydraForge.Tui.Renderers;

public class StatusBarRenderer
{
    public Panel Build(AppState appState)
    {
        var left = BuildConnectionIndicator(appState.Connection);
        var center = BuildNotificationCount(appState.UnreadNotifications);
        var right = BuildPresenceAndErrors(appState.OnlineCount, appState.Errors.Count);

        var grid = new Grid()
            .AddColumn(new GridColumn().LeftAligned())
            .AddColumn(new GridColumn().Centered())
            .AddColumn(new GridColumn().RightAligned());

        grid.AddRow(
            new Markup(left),
            new Markup(center),
            new Markup(right)
        );

        return new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            BorderColor = Color.Grey,
            Expand = true,
        };
    }

    private static string BuildConnectionIndicator(ConnectionStatus status) => status switch
    {
        ConnectionStatus.Connected => "[green]● Connected[/]",
        ConnectionStatus.Reconnecting => "[yellow]○ Reconnecting...[/]",
        ConnectionStatus.Disconnected => "[red]○ Disconnected[/]",
        _ => "[grey]○ Unknown[/]"
    };

    private static string BuildNotificationCount(int count) =>
        count > 0
            ? $"[yellow]{count} unread notification{(count > 1 ? "s" : "")}[/]"
            : "";

    private static string BuildPresenceAndErrors(int onlineCount, int errorCount)
    {
        var parts = new List<string>();

        if (onlineCount > 0)
            parts.Add($"[green]{onlineCount} online[/]");

        if (errorCount > 0)
            parts.Add($"[red]⚠ {errorCount} err{(errorCount > 1 ? "s" : "")}[/]");

        return string.Join("  |  ", parts);
    }
}
```

## Step 2: Create `ErrorPanelScreen`

Create `src/HydraForge.Tui/Screens/ErrorPanelScreen.cs`:

```csharp
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class ErrorPanelScreen : IScreen
{
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private int _selectedIndex;

    public ErrorPanelScreen(AppState appState, ErrorCollector errorCollector)
    {
        _appState = appState;
        _errorCollector = errorCollector;
    }

    public Task OnEnterAsync() => Task.CompletedTask;
    public Task OnExitAsync() => Task.CompletedTask;

    public Task RenderAsync()
    {
        var errors = _errorCollector.GetErrors();

        if (errors.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No errors.[/]");
            AnsiConsole.MarkupLine("[grey]Press Esc to close[/]");
            return Task.CompletedTask;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Correlation ID")
            .AddColumn("Message")
            .AddColumn("Time");

        for (int i = 0; i < errors.Count; i++)
        {
            var e = errors[i];
            var isSelected = i == _selectedIndex;
            var prefix = isSelected ? "[blue]>[/]" : " ";

            var corrId = e.CorrelationId.Length > 12
                ? e.CorrelationId[..12] + "..."
                : e.CorrelationId;

            var message = e.Message.Length > 60
                ? e.Message[..57] + "..."
                : e.Message;

            table.AddRow(
                $"{prefix} {i + 1}",
                $"[grey]{corrId}[/]",
                Markup.Escape(message),
                $"[grey]{e.Timestamp:HH:mm:ss}[/]"
            );
        }

        var panel = new Panel(table)
        {
            Border = BoxBorder.Heavy,
            BorderColor = Color.Red,
            Header = new PanelHeader($" Errors ({errors.Count}) "),
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[grey][[j/k]] Navigate  [[Del]] Dismiss  [[Esc]] Close[/]");

        return Task.CompletedTask;
    }

    public Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        var errors = _errorCollector.GetErrors();

        switch (key.Key)
        {
            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (_selectedIndex < errors.Count - 1)
                    _selectedIndex++;
                break;

            case ConsoleKey.K or ConsoleKey.UpArrow:
                if (_selectedIndex > 0)
                    _selectedIndex--;
                break;

            case ConsoleKey.Delete:
                if (_selectedIndex < errors.Count)
                {
                    _errorCollector.Dismiss(_selectedIndex);
                    if (_selectedIndex >= errors.Count - 1 && _selectedIndex > 0)
                        _selectedIndex--;
                }
                break;

            case ConsoleKey.Escape:
                _appState.CurrentScreen = null;
                return Task.CompletedTask;
        }

        return RenderAsync();
    }
}
```

## Step 3: Update `BoardRenderer` to use `StatusBarRenderer`

Update `src/HydraForge.Tui/Renderers/BoardRenderer.cs` — change `BuildLayout` signature to accept `AppState`:

```csharp
public Layout BuildLayout(
    List<ColumnData> columns,
    int selectedColumn,
    int selectedCard,
    string projectName,
    int totalCards,
    AppState appState)
{
    // ... existing layout code ...

    // Status bar
    var statusBar = new StatusBarRenderer();
    layout["Status"].Update(statusBar.Build(appState));

    return layout;
}
```

## Step 4: Update `BoardScreen.RenderAsync` to pass `AppState`

```csharp
public async Task RenderAsync()
{
    AnsiConsole.Clear();

    var totalCards = _columns.Sum(c => c.Cards.Count);
    var layout = _renderer.BuildLayout(
        _columns, _selectedColumn, _selectedCard, _projectName, totalCards, _appState);

    AnsiConsole.Write(layout);
    // ... footer ...
}
```

## Step 5: Wire error panel toggle in `KeyboardDispatcher`

Update `src/HydraForge.Tui/Services/KeyboardDispatcher.cs` — replace the `E` key placeholder:

```csharp
// 'E' — toggle error panel
if (key.Key == ConsoleKey.E)
{
    if (_screenStack.Peek() is ErrorPanelScreen)
    {
        _screenStack.Pop();
        return true;
    }

    var errorPanel = new ErrorPanelScreen(_appState, _errorCollector);
    _screenStack.Push(errorPanel);
    await errorPanel.RenderAsync();
    return true;
}
```

Add `ErrorCollector` dependency to `KeyboardDispatcher`:
```csharp
private readonly ErrorCollector _errorCollector;

public KeyboardDispatcher(AppState appState, ScreenStack screenStack, ErrorCollector errorCollector)
{
    _appState = appState;
    _screenStack = screenStack;
    _errorCollector = errorCollector;
}
```

## Step 6: Update `Program.cs` to pass `ErrorCollector` to `KeyboardDispatcher`

```csharp
var keyboardDispatcher = new KeyboardDispatcher(appState, screenStack, errorCollector);
```

## Step 7: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 8: Commit

```bash
git add src/HydraForge.Tui/Renderers/StatusBarRenderer.cs src/HydraForge.Tui/Screens/ErrorPanelScreen.cs src/HydraForge.Tui/Renderers/BoardRenderer.cs src/HydraForge.Tui/Screens/BoardScreen.cs src/HydraForge.Tui/Services/KeyboardDispatcher.cs src/HydraForge.Tui/Program.cs
git commit -m "feat(tui): add status bar with connection, presence, error panel"
```