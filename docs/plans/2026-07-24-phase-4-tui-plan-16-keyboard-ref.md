# Plan 16: Keyboard Shortcut Reference (`?` Overlay)

**Branch:** `task/tui-keyboard-ref`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 16

**Goal:** Overlay panel showing all keyboard shortcuts, toggled with `?`, dismissed with `?` or `Esc`.

**Depends on:** Task 1 (IScreen, AppState).

---

## Step 1: Create `KeyboardReferenceScreen`

Create `src/HydraForge.Tui/Screens/KeyboardReferenceScreen.cs`:

```csharp
using HydraForge.Tui.Models;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class KeyboardReferenceScreen : IScreen
{
    private readonly AppState _appState;

    public KeyboardReferenceScreen(AppState appState)
    {
        _appState = appState;
    }

    public Task OnEnterAsync() => Task.CompletedTask;
    public Task OnExitAsync() => Task.CompletedTask;

    public Task RenderAsync()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Key")
            .AddColumn("Action")
            .AddColumn("Context");

        // Global
        table.AddRow("[bold]?[/]", "Toggle this help", "Any");
        table.AddRow("[bold]E[/]", "Toggle error panel", "Any");
        table.AddRow("[bold]q[/]", "Quit (with confirm)", "Any");
        table.AddRow("[bold]Ctrl+C[/]", "Quit immediately", "Any");
        table.AddRow("[bold]Esc[/]", "Back / close overlay", "Any");

        table.AddEmptyRow();

        // Project List
        table.AddRow("[bold]j / Down[/]", "Move selection down", "Project List");
        table.AddRow("[bold]k / Up[/]", "Move selection up", "Project List");
        table.AddRow("[bold]Enter[/]", "Open project board", "Project List");
        table.AddRow("[bold]c[/]", "Create new project", "Project List");
        table.AddRow("[bold]/[/]", "Search/filter", "Project List");
        table.AddRow("[bold]a[/]", "Toggle archived", "Project List");
        table.AddRow("[bold]g[/]", "Go to first", "Project List");
        table.AddRow("[bold]G[/]", "Go to last", "Project List");

        table.AddEmptyRow();

        // Board View
        table.AddRow("[bold]h / Left[/]", "Previous column", "Board");
        table.AddRow("[bold]l / Right[/]", "Next column", "Board");
        table.AddRow("[bold]j / Down[/]", "Next card", "Board");
        table.AddRow("[bold]k / Up[/]", "Previous card", "Board");
        table.AddRow("[bold]Enter[/]", "Open card detail", "Board");
        table.AddRow("[bold]n[/]", "New card", "Board");
        table.AddRow("[bold]m[/]", "Move card", "Board");
        table.AddRow("[bold]r[/]", "Reorder card", "Board");
        table.AddRow("[bold]e[/]", "Edit card title", "Board");
        table.AddRow("[bold]d[/]", "Dependency panel", "Board");
        table.AddRow("[bold]Del[/]", "Archive card", "Board");
        table.AddRow("[bold]/[/]", "Filter cards", "Board");
        table.AddRow("[bold]Tab[/]", "Cycle focus", "Board");

        table.AddEmptyRow();

        // Card Detail
        table.AddRow("[bold]Tab[/]", "Next section", "Card Detail");
        table.AddRow("[bold]Shift+Tab[/]", "Previous section", "Card Detail");
        table.AddRow("[bold]e[/]", "Edit current section", "Card Detail");
        table.AddRow("[bold]Space[/]", "Toggle checklist item", "Card Detail");
        table.AddRow("[bold]a[/]", "Add comment", "Card Detail");
        table.AddRow("[bold]d[/]", "Add dependency", "Card Detail");
        table.AddRow("[bold]s[/]", "Open spec/plan viewer", "Card Detail");
        table.AddRow("[bold]Esc[/]", "Back to board", "Card Detail");

        var panel = new Panel(table)
        {
            Border = BoxBorder.Heavy,
            BorderColor = Color.Blue,
            Header = new PanelHeader(" Keyboard Shortcuts "),
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[grey]Press [?] or [Esc] to close[/]");

        return Task.CompletedTask;
    }

    public Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Oem2 || key.Key == ConsoleKey.Divide || // '?'
            key.Key == ConsoleKey.Escape)
        {
            _appState.CurrentScreen = null; // Dismiss overlay
        }
        return Task.CompletedTask;
    }
}
```

## Step 2: Create `KeyboardDispatcher` service

Create `src/HydraForge.Tui/Services/KeyboardDispatcher.cs`:

```csharp
using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;

namespace HydraForge.Tui.Services;

public class KeyboardDispatcher
{
    private readonly AppState _appState;
    private readonly ScreenStack _screenStack;

    public KeyboardDispatcher(AppState appState, ScreenStack screenStack)
    {
        _appState = appState;
        _screenStack = screenStack;
    }

    public async Task<bool> HandleGlobalKeyAsync(ConsoleKeyInfo key)
    {
        // '?' — toggle keyboard reference
        if (key.Key == ConsoleKey.Oem2 || key.Key == ConsoleKey.Divide)
        {
            if (_screenStack.Peek() is KeyboardReferenceScreen)
            {
                _screenStack.Pop();
                return true;
            }

            var refScreen = new KeyboardReferenceScreen(_appState);
            _screenStack.Push(refScreen);
            await refScreen.RenderAsync();
            return true;
        }

        // 'E' — toggle error panel (Task 17)
        if (key.Key == ConsoleKey.E)
        {
            // Error panel toggle — Task 17
            return true;
        }

        // Ctrl+C — quit immediately
        if (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0)
        {
            Environment.Exit(0);
        }

        return false;
    }
}
```

## Step 3: Wire `KeyboardDispatcher` into `Program.cs` main loop

Update `src/HydraForge.Tui/Program.cs` — replace the main input loop:

```csharp
var keyboardDispatcher = new KeyboardDispatcher(appState, screenStack);

// Main input loop
while (true)
{
    var key = Console.ReadKey(true);

    // Global shortcuts first
    if (await keyboardDispatcher.HandleGlobalKeyAsync(key))
    {
        // If overlay was shown, re-render current screen after overlay dismissed
        if (screenStack.Count == 0 && appState.CurrentScreen != null)
        {
            await appState.CurrentScreen.RenderAsync();
        }
        continue;
    }

    // Route to overlay if active
    if (screenStack.Count > 0)
    {
        var overlay = screenStack.Peek();
        if (overlay != null)
        {
            await overlay.HandleKeyAsync(key);
            if (screenStack.Count == 0 && appState.CurrentScreen != null)
            {
                await appState.CurrentScreen.RenderAsync();
            }
            continue;
        }
    }

    // Route to current screen
    if (appState.CurrentScreen != null)
    {
        await appState.CurrentScreen.HandleKeyAsync(key);
    }
}
```

## Step 4: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 5: Commit

```bash
git add src/HydraForge.Tui/Screens/KeyboardReferenceScreen.cs src/HydraForge.Tui/Services/KeyboardDispatcher.cs src/HydraForge.Tui/Program.cs
git commit -m "feat(tui): add keyboard shortcut reference overlay and global key dispatcher"
```