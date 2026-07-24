# Plan 8: SignalR Integration — BoardHub + PresenceHub

**Branch:** `task/tui-signalr`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 8

**Goal:** Connect to BoardHub and PresenceHub on board enter, handle real-time events, update LiveDisplay, manage reconnect lifecycle.

**Depends on:** Task 5 (ConnectionManager), Task 7 (BoardScreen with BoardRenderer).

---

## Step 1: Create `SignalRConnectionManager`

Create `src/HydraForge.Tui/Services/SignalRConnectionManager.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using HydraForge.Tui.Models;
using HydraForge.Tui.Renderers;

namespace HydraForge.Tui.Services;

public class SignalRConnectionManager : IAsyncDisposable
{
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;

    private HubConnection? _boardConnection;
    private HubConnection? _presenceConnection;

    public event Action<BoardEvent>? OnBoardEvent;
    public event Action<List<PresenceUser>>? OnCurrentUsers;
    public event Action<PresenceUser>? OnUserJoined;
    public event Action<PresenceUser>? OnUserLeft;
    public event Action<Guid, Guid>? OnCardFocused;
    public event Action<Guid>? OnCardUnfocused;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SignalRConnectionManager(
        ApiClientFactory apiClientFactory,
        AppState appState,
        ErrorCollector errorCollector)
    {
        _apiClientFactory = apiClientFactory;
        _appState = appState;
        _errorCollector = errorCollector;
    }

    public async Task ConnectAsync(Guid projectId)
    {
        var config = new ConfigStore().Load();
        var serverUrl = config.ServerUrl.TrimEnd('/');
        var token = config.JwtToken ?? "";

        // BoardHub connection
        _boardConnection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/board", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect(new[] {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _boardConnection.On<JsonElement>("OnBoardEvent", envelope =>
        {
            var evt = JsonSerializer.Deserialize<BoardEvent>(envelope.GetRawText(), JsonOptions);
            if (evt != null)
                OnBoardEvent?.Invoke(evt);
        });

        _boardConnection.Reconnecting += _ =>
        {
            _appState.Connection = ConnectionStatus.Reconnecting;
            return Task.CompletedTask;
        };

        _boardConnection.Reconnected += async _ =>
        {
            _appState.Connection = ConnectionStatus.Connected;
            await _boardConnection.InvokeAsync("JoinProject", projectId);
        };

        _boardConnection.Closed += _ =>
        {
            _appState.Connection = ConnectionStatus.Disconnected;
            return Task.CompletedTask;
        };

        await _boardConnection.StartAsync();
        await _boardConnection.InvokeAsync("JoinProject", projectId);

        // PresenceHub connection
        _presenceConnection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/presence", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        _presenceConnection.On<JsonElement>("CurrentUsers", users =>
        {
            var list = JsonSerializer.Deserialize<List<PresenceUser>>(users.GetRawText(), JsonOptions);
            if (list != null)
                OnCurrentUsers?.Invoke(list);
        });

        _presenceConnection.On<JsonElement>("UserJoined", user =>
        {
            var u = JsonSerializer.Deserialize<PresenceUser>(user.GetRawText(), JsonOptions);
            if (u != null)
                OnUserJoined?.Invoke(u);
        });

        _presenceConnection.On<JsonElement>("UserLeft", user =>
        {
            var u = JsonSerializer.Deserialize<PresenceUser>(user.GetRawText(), JsonOptions);
            if (u != null)
                OnUserLeft?.Invoke(u);
        });

        _presenceConnection.On<JsonElement>("CardFocused", data =>
        {
            var focus = JsonSerializer.Deserialize<CardFocusData>(data.GetRawText(), JsonOptions);
            if (focus != null)
                OnCardFocused?.Invoke(focus.UserId, focus.CardId);
        });

        _presenceConnection.On<JsonElement>("CardUnfocused", data =>
        {
            var unfocus = JsonSerializer.Deserialize<CardUnfocusData>(data.GetRawText(), JsonOptions);
            if (unfocus != null)
                OnCardUnfocused?.Invoke(unfocus.UserId);
        });

        await _presenceConnection.StartAsync();
        await _presenceConnection.InvokeAsync("JoinProject", projectId);

        _appState.Connection = ConnectionStatus.Connected;
    }

    public async Task DisconnectAsync()
    {
        if (_boardConnection != null)
        {
            await _boardConnection.StopAsync();
            await _boardConnection.DisposeAsync();
        }
        if (_presenceConnection != null)
        {
            await _presenceConnection.StopAsync();
            await _presenceConnection.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    // Event DTOs
    public record BoardEvent(
        Guid EventId, Guid ProjectId, string EntityType, Guid EntityId,
        string Action, int Version, DateTime OccurredAt, JsonElement Payload
    );

    public record PresenceUser(Guid UserId, string Username, string ConnectionId);
    public record CardFocusData(Guid UserId, Guid CardId, string ConnectionId);
    public record CardUnfocusData(Guid UserId, string ConnectionId);
}
```

## Step 2: Integrate SignalR into `BoardScreen`

Update `src/HydraForge.Tui/Screens/BoardScreen.cs` — add SignalR field and wire in `OnEnterAsync`/`OnExitAsync`:

Add field:
```csharp
private SignalRConnectionManager? _signalR;
```

Update `OnEnterAsync`:
```csharp
public async Task OnEnterAsync()
{
    _projectId = _appState.SelectedProjectId ?? Guid.Empty;
    await LoadBoardAsync();

    // Connect SignalR
    _signalR = new SignalRConnectionManager(_apiClientFactory, _appState, _errorCollector);
    _signalR.OnBoardEvent += HandleBoardEvent;
    _signalR.OnCurrentUsers += users => _appState.OnlineCount = users.Count;
    _signalR.OnUserJoined += _ => _appState.OnlineCount++;
    _signalR.OnUserLeft += _ => _appState.OnlineCount = Math.Max(0, _appState.OnlineCount - 1);

    await _signalR.ConnectAsync(_projectId);
}
```

Update `OnExitAsync`:
```csharp
public async Task OnExitAsync()
{
    if (_signalR != null)
        await _signalR.DisconnectAsync();
}
```

Add event handler:
```csharp
private async void HandleBoardEvent(SignalRConnectionManager.BoardEvent evt)
{
    // Reload board data on any event for simplicity
    // (Future optimization: apply delta updates)
    await LoadBoardAsync();
    await RenderAsync();
}
```

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Services/SignalRConnectionManager.cs src/HydraForge.Tui/Screens/BoardScreen.cs
git commit -m "feat(tui): add SignalR integration for BoardHub and PresenceHub"
```