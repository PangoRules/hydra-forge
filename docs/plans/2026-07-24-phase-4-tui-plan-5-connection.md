# Plan 5: Connection Handling — Lock Screen + Auto-Reconnect

**Branch:** `task/tui-connection`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 5

**Goal:** Lock screen overlay when server unreachable, auto-reconnect with backoff, connection status tracking in AppState.

**Depends on:** Task 1 (AppState, IScreen), Task 3 (ApiClientFactory wraps `HydraForgeApiClient`).

---

## Step 1: Create `LockScreen`

Create `src/HydraForge.Tui/Screens/LockScreen.cs`:

```csharp
using HydraForge.Tui.Models;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class LockScreen : IScreen
{
    private readonly AppState _appState;
    private readonly Func<Task<bool>> _healthCheck;
    private CancellationTokenSource? _retryCts;
    private int _retryAttempt;

    public LockScreen(AppState appState, Func<Task<bool>> healthCheck)
    {
        _appState = appState;
        _healthCheck = healthCheck;
    }

    public Task OnEnterAsync()
    {
        _retryAttempt = 0;
        _retryCts = new CancellationTokenSource();
        _ = RetryLoopAsync(_retryCts.Token);
        return Task.CompletedTask;
    }

    public Task OnExitAsync()
    {
        _retryCts?.Cancel();
        return Task.CompletedTask;
    }

    public Task RenderAsync()
    {
        AnsiConsole.Clear();

        var panel = new Panel(
            Align.Center(
                new Rows(
                    new Markup("[yellow]⚠ Server Unreachable[/]"),
                    new Markup("[grey]Retrying...[/]"),
                    new Markup($"[grey]Attempt {_retryAttempt}[/]")
                )
            )
        )
        {
            Border = BoxBorder.Heavy,
            BorderColor = Color.Yellow,
            Header = new PanelHeader(" HydraForge "),
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press [bold]q[/] to quit[/]");

        return Task.CompletedTask;
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Q)
        {
            var confirm = AnsiConsole.Confirm("Quit HydraForge?");
            if (confirm)
            {
                _retryCts?.Cancel();
                Environment.Exit(0);
            }
        }
    }

    private async Task RetryLoopAsync(CancellationToken ct)
    {
        int[] delays = [5000, 10000, 30000, 60000];

        while (!ct.IsCancellationRequested)
        {
            _retryAttempt++;

            try
            {
                var healthy = await _healthCheck();
                if (healthy)
                {
                    _appState.Connection = ConnectionStatus.Connected;
                    return; // Lock screen dismissed by caller
                }
            }
            catch
            {
                // Still unreachable
            }

            _appState.Connection = ConnectionStatus.Reconnecting;

            var delay = _retryAttempt <= delays.Length
                ? delays[_retryAttempt - 1]
                : 60000;

            await Task.Delay(delay, ct);
        }
    }
}
```

## Step 2: Create `ConnectionManager` service

Create `src/HydraForge.Tui/Services/ConnectionManager.cs`:

```csharp
using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public class ConnectionManager
{
    private readonly AppState _appState;
    private readonly ApiClientFactory _apiClientFactory;
    private readonly ErrorCollector _errorCollector;

    public ConnectionManager(
        AppState appState,
        ApiClientFactory apiClientFactory,
        ErrorCollector errorCollector)
    {
        _appState = appState;
        _apiClientFactory = apiClientFactory;
        _errorCollector = errorCollector;
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            // NSwag generates HealthAsync() from the /api/health endpoint
            await client.HealthAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public LockScreen CreateLockScreen()
    {
        return new LockScreen(_appState, CheckHealthAsync);
    }

    public async Task<bool> WaitForConnectionAsync(int timeoutMs = 30000)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (await CheckHealthAsync())
            {
                _appState.Connection = ConnectionStatus.Connected;
                return true;
            }
            _appState.Connection = ConnectionStatus.Reconnecting;
            await Task.Delay(2000);
        }
        _appState.Connection = ConnectionStatus.Disconnected;
        return false;
    }
}
```

**Key change:** `CheckHealthAsync` uses `client.HealthAsync()` (NSwag-generated typed method) instead of `client.GetAsync("api/health")`. No raw `HttpClient` calls.

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Screens/LockScreen.cs src/HydraForge.Tui/Services/ConnectionManager.cs
git commit -m "feat(tui): add lock screen and connection manager with auto-retry"
```