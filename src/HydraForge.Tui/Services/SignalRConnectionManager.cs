using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using HydraForge.Tui.Models;
using HydraForge.Tui.Renderers;

namespace HydraForge.Tui.Services;

public class SignalRConnectionManager : IAsyncDisposable
{
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;

    private HubConnection? _boardConnection;
    private HubConnection? _presenceConnection;
    private HubConnection? _notificationConnection;
    private Guid _currentProjectId;

    public event Action<BoardEvent>? OnBoardEvent;
    public event Action<List<PresenceUser>>? OnCurrentUsers;
    public event Action<PresenceUser>? OnUserJoined;
    public event Action<PresenceUser>? OnUserLeft;
    public event Action<Guid, Guid>? OnCardFocused;
    public event Action<Guid>? OnCardUnfocused;
    public event Action<int>? OnUnreadCountChanged;
    protected virtual void RaiseUnreadCountChanged(int count) => OnUnreadCountChanged?.Invoke(count);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SignalRConnectionManager(
        AppState appState,
        ErrorCollector errorCollector)
    {
        _appState = appState;
        _errorCollector = errorCollector;
    }

    public async Task ConnectAsync(Guid projectId)
    {
        // Already connected to this project — nothing to do.
        if (_currentProjectId == projectId && _boardConnection != null)
            return;

        // Switching projects — tear down old board/presence connections first.
        // NotificationHub is user-scoped, not project-scoped, and has its own
        // lifecycle via ConnectNotificationsAsync — leave it alone here.
        await DisconnectAsync();
        _currentProjectId = projectId;

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
            try
            {
                var evt = JsonSerializer.Deserialize<BoardEvent>(envelope.GetRawText(), JsonOptions);
                if (evt != null)
                    OnBoardEvent?.Invoke(evt);
            }
            catch (JsonException ex)
            {
                _errorCollector.Add(Guid.NewGuid().ToString("N")[..8], $"Board event deserialize failed: {ex.Message}");
            }
        });

        _boardConnection.Reconnecting += _ =>
        {
            _appState.Connection = ConnectionStatus.Reconnecting;
            return Task.CompletedTask;
        };

        _boardConnection.Reconnected += async _ =>
        {
            _appState.Connection = ConnectionStatus.Connected;
            try
            {
                await _boardConnection.InvokeAsync("JoinProject", projectId);
            }
            catch (Exception ex)
            {
                _errorCollector.Add("N/A", $"Board rejoin after reconnect failed: {ex.Message}");
            }
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

        // Reconnect gives the connection a new ConnectionId server-side, so group
        // membership and the presence-tracking dictionary entry are gone until
        // JoinProject runs again — without this the user goes dark for presence
        // (no online count updates, no card focus events) after any network blip.
        _presenceConnection.Reconnected += async _ =>
        {
            try
            {
                await _presenceConnection.InvokeAsync("JoinProject", projectId);
            }
            catch (Exception ex)
            {
                _errorCollector.Add("N/A", $"Presence rejoin after reconnect failed: {ex.Message}");
            }
        };

        await _presenceConnection.StartAsync();
        await _presenceConnection.InvokeAsync("JoinProject", projectId);

        _appState.Connection = ConnectionStatus.Connected;
    }

    // NotificationHub is user-scoped, not project-scoped — connect once, before any
    // project is even opened (ProjectListScreen.OnEnterAsync), and leave it running for
    // the app's lifetime. Idempotent: safe to call again on every re-entry to the project list.
    public async Task ConnectNotificationsAsync()
    {
        if (_notificationConnection != null)
            return;

        var config = new ConfigStore().Load();
        var serverUrl = config.ServerUrl.TrimEnd('/');
        var token = config.JwtToken ?? "";

        _notificationConnection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/notifications", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        _notificationConnection.On<JsonElement>("OnNotificationReceived", envelope =>
        {
            var notification = JsonSerializer.Deserialize<NotificationReceivedEvent>(envelope.GetRawText(), JsonOptions);
            if (notification != null && !notification.IsRead)
            {
                var newCount = _appState.IncrementUnreadNotifications();
                OnUnreadCountChanged?.Invoke(newCount);
            }
        });

        await _notificationConnection.StartAsync();
    }

    // Tears down the project-scoped BoardHub/PresenceHub connections. NotificationHub is
    // intentionally untouched — it outlives project navigation (see ConnectNotificationsAsync).
    public async Task DisconnectAsync()
    {
        if (_boardConnection != null)
        {
            await _boardConnection.StopAsync();
            await _boardConnection.DisposeAsync();
            _boardConnection = null;
        }
        if (_presenceConnection != null)
        {
            await _presenceConnection.StopAsync();
            await _presenceConnection.DisposeAsync();
            _presenceConnection = null;
        }
        _currentProjectId = Guid.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        if (_notificationConnection != null)
        {
            await _notificationConnection.StopAsync();
            await _notificationConnection.DisposeAsync();
            _notificationConnection = null;
        }
    }

    // Event DTOs
    public record BoardEvent(
        Guid EventId, Guid ProjectId, string EntityType, Guid EntityId,
        string Action, int Version, DateTime OccurredAt, JsonElement Payload
    );

    public record PresenceUser(Guid UserId, string Username, string ConnectionId);
    public record CardFocusData(Guid UserId, Guid CardId, string ConnectionId);
    public record CardUnfocusData(Guid UserId, string ConnectionId);

    public record NotificationReceivedEvent(
        Guid Id,
        string Title,
        string? Body,
        Guid? CardId,
        Guid? ProjectId,
        string? ActionUrl,
        DateTime CreatedAt,
        bool IsRead
    );
}