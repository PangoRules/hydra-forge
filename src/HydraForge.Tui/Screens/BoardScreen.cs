using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Renderers;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class BoardScreen(
    ApiClientFactory apiClientFactory,
    AppState appState,
    ErrorCollector errorCollector,
    ConnectionManager connectionManager,
    SignalRConnectionManager signalRConnectionManager,
    NotificationCenter notificationCenter
) : IScreen
{
    private readonly ApiClientFactory _apiClientFactory = apiClientFactory;
    private readonly AppState _appState = appState;
    private readonly ErrorCollector _errorCollector = errorCollector;
    private readonly ConnectionManager _connectionManager = connectionManager;
    private readonly SignalRConnectionManager _signalRConnectionManager = signalRConnectionManager;
    private readonly NotificationCenter _notificationCenter = notificationCenter;
    private HydraForgeApiClient Client => _apiClientFactory.GetClient();
    private readonly BoardRenderer _renderer = new();
    private readonly SemaphoreSlim _renderLock = new(1, 1);
    private bool _signalRSubscribed;

    private List<BoardRenderer.ColumnData> _columns = [];
    private int _selectedColumn;
    private int _selectedCard;
    private string _projectName = "";
    private Guid _projectId;
    private bool _reorderMode = false;
    private Guid? _reorderCardId;

    public async Task OnEnterAsync()
    {
        _projectId = _appState.SelectedProjectId ?? Guid.Empty;

        await LoadBoardAsync();

        if (_appState.BoardCursorCol.HasValue && _appState.BoardCursorCard.HasValue)
        {
            _selectedColumn = Math.Clamp(
                _appState.BoardCursorCol.Value,
                0,
                Math.Max(0, _columns.Count - 1)
            );
            _selectedCard = _appState.BoardCursorCard.Value;
            if (_selectedColumn < _columns.Count)
            {
                var col = _columns[_selectedColumn];
                _selectedCard = Math.Clamp(_selectedCard, 0, Math.Max(0, col.Cards.Count - 1));
            }
            _appState.BoardCursorCol = null;
            _appState.BoardCursorCard = null;
        }

        // Connect SignalR — real-time sync degrades gracefully if the server is unreachable.
        // SignalRConnectionManager is a singleton shared across screen instances; event
        // handlers are subscribed once per instance (guarded, since OnEnterAsync can run
        // again on the same instance after an overlay dismiss) and unsubscribed in
        // OnExitAsync so a discarded BoardScreen instance doesn't leak stale closures onto
        // the singleton's event list.
        if (!_signalRSubscribed)
        {
            _signalRConnectionManager.OnBoardEvent += HandleBoardEvent;
            _signalRConnectionManager.OnCurrentUsers += HandleCurrentUsers;
            _signalRConnectionManager.OnUserJoined += HandleUserJoined;
            _signalRConnectionManager.OnUserLeft += HandleUserLeft;
            _signalRConnectionManager.OnCardFocused += HandleCardFocused;
            _signalRConnectionManager.OnCardUnfocused += HandleCardUnfocused;
            _signalRConnectionManager.OnUnreadCountChanged += HandleUnreadCountChanged;
            _signalRSubscribed = true;
        }

        try
        {
            await _signalRConnectionManager.ConnectAsync(_projectId);
        }
        catch (Exception ex)
        {
            _appState.Connection = ConnectionStatus.Disconnected;
            _errorCollector.Add("N/A", $"Real-time connection failed: {ex.Message}");
        }

        // NotificationHub connects once, in ProjectListScreen, before any board is opened —
        // just refresh the count here in case it changed while we were elsewhere.
        await _notificationCenter.FetchUnreadCountAsync();
    }

    public async Task OnExitAsync()
    {
        if (_signalRSubscribed)
        {
            _signalRConnectionManager.OnBoardEvent -= HandleBoardEvent;
            _signalRConnectionManager.OnCurrentUsers -= HandleCurrentUsers;
            _signalRConnectionManager.OnUserJoined -= HandleUserJoined;
            _signalRConnectionManager.OnUserLeft -= HandleUserLeft;
            _signalRConnectionManager.OnCardFocused -= HandleCardFocused;
            _signalRConnectionManager.OnCardUnfocused -= HandleCardUnfocused;
            _signalRConnectionManager.OnUnreadCountChanged -= HandleUnreadCountChanged;
            _signalRSubscribed = false;
        }
        await _signalRConnectionManager.DisconnectAsync();
    }

    public async Task RenderAsync()
    {
        // Board mutations, presence updates, and key input can all trigger a render
        // concurrently from independent SignalR callback threads — without this lock,
        // overlapping Clear()+Write() calls interleave and leave stacked/duplicate
        // frames (e.g. the key hint bar printing multiple times).
        await _renderLock.WaitAsync();
        try
        {
            AnsiConsole.Clear();

            var totalCards = _columns.Sum(c => c.Cards.Count);
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
                _appState.UnreadNotifications
            );

            AnsiConsole.Write(layout);

            if (_reorderMode)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Reorder mode: j/k to place the highlighted card, Enter to confirm, Esc to cancel[/]"
                );
            }

            KeyHintBar.Render([
                "[h/l] Columns",
                "[j/k] Cards",
                "[Enter] Detail",
                "[n] New",
                "[e] Edit",
                "[m] Move",
                "[r] Reorder",
                "[u] Notifications",
                "[Del] Archive",
                "[x] Errors",
                "[?] Help",
                "[Esc] Back",
                "[q] Quit",
            ]);
        }
        finally
        {
            _renderLock.Release();
        }
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.H or ConsoleKey.LeftArrow:
                if (!_reorderMode && _selectedColumn > 0)
                {
                    _selectedColumn--;
                    _selectedCard = 0;
                }
                await RenderAsync();
                break;

            case ConsoleKey.L
            or ConsoleKey.RightArrow:
                if (!_reorderMode && _selectedColumn < _columns.Count - 1)
                {
                    _selectedColumn++;
                    _selectedCard = 0;
                }
                await RenderAsync();
                break;

            case ConsoleKey.J
            or ConsoleKey.DownArrow:
            case ConsoleKey.N when key.Modifiers == ConsoleModifiers.Control:
                if (_columns.Count > 0)
                {
                    var col = _columns[_selectedColumn];
                    if (_selectedCard < col.Cards.Count - 1)
                        _selectedCard++;
                }
                await RenderAsync();
                break;

            case ConsoleKey.K
            or ConsoleKey.UpArrow:
            case ConsoleKey.P when key.Modifiers == ConsoleModifiers.Control:
                if (_selectedCard > 0)
                    _selectedCard--;
                await RenderAsync();
                break;

            case ConsoleKey.G when key.Modifiers == ConsoleModifiers.Shift:
                _selectedColumn = Math.Max(0, _columns.Count - 1);
                _selectedCard = 0;
                await RenderAsync();
                break;

            case ConsoleKey.G:
                _selectedColumn = 0;
                _selectedCard = 0;
                await RenderAsync();
                break;

            case ConsoleKey.Enter:
                if (_reorderMode)
                {
                    await ConfirmReorderAsync();
                }
                else
                {
                    await OpenCardDetailAsync();
                }
                break;

            case ConsoleKey.Escape:
                if (_reorderMode)
                {
                    _reorderMode = false;
                    _reorderCardId = null;
                    await RenderAsync();
                }
                else
                {
                    await OnExitAsync();
                    var projectListScreen = new ProjectListScreen(
                        _apiClientFactory,
                        _appState,
                        _errorCollector,
                        _connectionManager,
                        _signalRConnectionManager,
                        _notificationCenter
                    );
                    _appState.CurrentScreen = projectListScreen;
                    _appState.SelectedProjectId = null;
                    await projectListScreen.OnEnterAsync();
                    await projectListScreen.RenderAsync();
                }
                break;

            case ConsoleKey.E:
                if (!_reorderMode)
                {
                    await EditCardTitleAsync();
                }
                break;

            case ConsoleKey.R:
                if (!_reorderMode)
                {
                    await EnterReorderModeAsync();
                }
                break;

            case ConsoleKey.N:
                await CreateCardAsync();
                break;

            case ConsoleKey.M:
                await MoveCardAsync();
                break;

            case ConsoleKey.D:
                if (_columns.Count > 0)
                {
                    var col = _columns[_selectedColumn];
                    if (_selectedCard < col.Cards.Count)
                    {
                        var card = col.Cards[_selectedCard];
                        _appState.PreviousScreen = this;
                        var depPanel = new DependencyPanel(
                            _apiClientFactory,
                            _appState,
                            _errorCollector,
                            _projectId,
                            card.Id
                        );
                        _appState.CurrentScreen = depPanel;
                        await depPanel.RenderAsync();
                    }
                }
                break;

            case ConsoleKey.Delete:
                await ArchiveCardAsync();
                break;

            case ConsoleKey.X:
                _appState.PreviousScreen = this;
                var errorPanel = new ErrorPanelScreen(_appState, _errorCollector);
                _appState.CurrentScreen = errorPanel;
                await errorPanel.RenderAsync();
                break;

            case ConsoleKey.U:
                await _notificationCenter.ShowNotificationsAsync(RenderAsync);
                break;

            case ConsoleKey.O:
                ShowOnlineUsers();
                await RenderAsync();
                break;

            case ConsoleKey.Q:
                var confirm = AnsiConsole.Confirm("Quit HydraForge?");
                if (confirm)
                    Environment.Exit(0);
                break;

            case ConsoleKey when key.KeyChar == '?':
                ShowHelp();
                await RenderAsync();
                break;
        }
    }

    private static void ShowHelp()
    {
        HelpOverlay.Show(
            "Board",
            [
                ("h/l, ←/→", "Prev/next column"),
                ("j/k, ↑/↓, Ctrl+n/p", "Prev/next card"),
                ("g / G", "First / last column"),
                ("Enter", "Open card detail (confirm reorder)"),
                ("n", "New card"),
                ("e", "Edit card title"),
                ("m", "Move card to column"),
                ("r", "Reorder mode (j/k to place, Enter to confirm, Esc to cancel)"),
                ("u", "Notifications"),
                ("o", "Online users"),
                ("Del", "Archive card"),
                ("d", "Add dependency"),
                ("x", "Error panel"),
                ("Esc", "Back to project list"),
                ("q", "Quit"),
                ("?", "This help"),
            ]
        );
    }

    private void ShowOnlineUsers()
    {
        var currentUserId = CurrentUser.GetId();
        var allCards = _columns.SelectMany(c => c.Cards).ToList();

        var users = _appState.OnlineUsers
            .Where(u => u.Key != currentUserId)
            .Select(u =>
            {
                int? cardNumber = _appState.FocusedCards.TryGetValue(u.Key, out var cardId)
                    ? allCards.FirstOrDefault(c => c.Id == cardId)?.CardNumber
                    : null;
                return (u.Value, cardNumber);
            })
            .ToList();

        OnlineUsersOverlay.Show(users);
    }

    private async Task LoadBoardAsync()
    {
        try
        {

            // Load project
            var project = await Client.ProjectsGET2Async(_projectId);
            _projectName = project.Name;

            // Load cards
            var cardList = await Client.CardsGETAsync(_projectId);
            var cards = cardList?.Cards ?? [];

            // Load relationship badges per card — non-fatal if it fails
            var cardBadges = new Dictionary<Guid, List<RelationBadge>>();
            try
            {
                foreach (var card in cards)
                {
                    var rels = await Client.CardRelationshipsGETAsync(_projectId, card.Id);
                    cardBadges[card.Id] = CardRelationshipIndicatorHelper.GetRelationBadges(
                        card.Id,
                        rels?.Relationships ?? []
                    );
                }
            }
            catch (Exception)
            {
                // Non-fatal: board renders without relationship indicators
            }

            // Parent lookup + child count are derived client-side from the already-loaded
            // card list, same as the web UI does — there's no dedicated endpoint for either.
            var cardsById = cards.ToDictionary(c => c.Id);
            var childCounts = cards
                .Where(c => c.ParentCardId.HasValue)
                .GroupBy(c => c.ParentCardId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
            var currentUserId = CurrentUser.GetId();

            // Build column data
            _columns =
            [
                .. project
                    .Columns.OrderBy(c => c.Position)
                    .Select(col => new BoardRenderer.ColumnData(
                        col.Id,
                        col.Name,
                        col.Position,
                        col.WipLimit,
                        col.Color,
                        [
                            .. cards
                                .Where(c => c.ColumnId == col.Id)
                                .OrderBy(c => c.Position)
                                .Select(c =>
                                {
                                    var parent = c.ParentCardId.HasValue
                                        ? cardsById.GetValueOrDefault(c.ParentCardId.Value)
                                        : null;

                                    return new BoardRenderer.CardData(
                                        c.Id,
                                        c.CardNumber,
                                        c.Title,
                                        CardTypeMapper.ToDisplayString(c.Type), // Convert enum to string
                                        cardBadges.GetValueOrDefault(c.Id, []),
                                        c.Assignees?.Select(a => a.Username[..1].ToUpper()).ToList()
                                            ?? [],
                                        c.Version,
                                        parent?.CardNumber,
                                        parent != null ? CardTypeMapper.ToDisplayString(parent.Type) : null,
                                        childCounts.GetValueOrDefault(c.Id, 0),
                                        c.DueAt,
                                        currentUserId.HasValue && (c.Watchers?.Any(w => w.UserId == currentUserId.Value) ?? false)
                                    );
                                }),
                        ]
                    )),
            ];
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Board load error: {ex.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Board load error: {ex.Message}");
        }
    }

    private async Task OpenCardDetailAsync()
    {
        if (_columns.Count == 0)
            return;
        var col = _columns[_selectedColumn];
        if (_selectedCard >= col.Cards.Count)
            return;

        var card = col.Cards[_selectedCard];
        _appState.SelectedCardId = card.Id;
        _appState.BoardCursorCol = _selectedColumn;
        _appState.BoardCursorCard = _selectedCard;

        await OnExitAsync();
        var detailScreen = new CardDetailScreen(
            _apiClientFactory,
            _appState,
            _errorCollector,
            _connectionManager,
            _signalRConnectionManager,
            _notificationCenter
        );
        _appState.CurrentScreen = detailScreen;
        await detailScreen.OnEnterAsync();
        await detailScreen.RenderAsync();
    }

    private async Task CreateCardAsync()
    {
        if (_columns.Count == 0)
            return;
        var col = _columns[_selectedColumn];

        var title = AnsiConsole.Prompt(
            new TextPrompt<string>("Card title:").Validate(t =>
                string.IsNullOrWhiteSpace(t)
                    ? ValidationResult.Error("Title required")
                    : ValidationResult.Success()
            )
        );

        var typeChoices = new[] { "Task", "Issue", "Goal", "Idea" };
        var typeIdx = await ListPrompt.Show("Type:", typeChoices, renderBackdrop: RenderAsync) ?? 0;
        var type = Enum.Parse<CardType>(typeChoices[typeIdx]);

        try
        {

            await Client.CardsPOSTAsync(
                _projectId,
                new CreateCardRequest
                {
                    ColumnId = col.Id,
                    Title = title,
                    Description = "",
                    Type = type,
                    ParentCardId = null,
                    DueAt = null,
                    AssigneeUserIds = [],
                }
            );

            await LoadBoardAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Create card failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task MoveCardAsync()
    {
        if (_columns.Count == 0)
            return;
        var sourceCol = _columns[_selectedColumn];
        if (_selectedCard >= sourceCol.Cards.Count)
            return;
        var card = sourceCol.Cards[_selectedCard];

        var targetNames = _columns.Select(c => c.Name).ToList();
        var targetIdx = await ListPrompt.Show(
            "Move to column:",
            targetNames,
            _selectedColumn,
            RenderAsync
        );
        if (!targetIdx.HasValue)
        {
            await RenderAsync();
            return;
        }

        var targetCol = _columns[targetIdx.Value];

        try
        {

            await Client.MoveAsync(
                _projectId,
                card.Id,
                new MoveCardRequest
                {
                    TargetColumnId = targetCol.Id,
                    TargetPosition = targetCol.Cards.Count,
                    ConfirmBlockedMove = false,
                    Version = card.Version,
                }
            );

            await LoadBoardAsync();
            await RenderAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Move blocked by dependencies. Use --force to override.[/]"
            );
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Move failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task ArchiveCardAsync()
    {
        if (_columns.Count == 0)
            return;
        var col = _columns[_selectedColumn];
        if (_selectedCard >= col.Cards.Count)
            return;
        var card = col.Cards[_selectedCard];

        var confirm = AnsiConsole.Confirm($"Archive card #{card.CardNumber}?");
        if (!confirm)
            return;

        try
        {

            await Client.ArchiveAsync(
                _projectId,
                card.Id,
                new ArchiveCardRequest { Version = card.Version }
            );

            await LoadBoardAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Archive error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Archive error: {ex.Message}");
        }
    }

    private async Task EditCardTitleAsync()
    {
        if (_columns.Count == 0)
            return;
        var col = _columns[_selectedColumn];
        if (_selectedCard >= col.Cards.Count)
            return;
        var card = col.Cards[_selectedCard];

        try
        {

            var newTitle = AnsiConsole.Prompt(
                new TextPrompt<string>($"New title (current: {card.Title}):")
                    .DefaultValue(card.Title)
                    .Validate(t =>
                        string.IsNullOrWhiteSpace(t)
                            ? ValidationResult.Error("Title required")
                            : ValidationResult.Success()
                    )
            );

            await Client.CardsPUTAsync(
                _projectId,
                card.Id,
                new UpdateCardRequest
                {
                    Title = newTitle,
                    Description = "",
                    Type = CardTypeMapper.FromDisplayString(card.Type),
                    ParentCardId = null,
                    DueAt = null,
                    Version = card.Version,
                }
            );

            await LoadBoardAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Update title failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task EnterReorderModeAsync()
    {
        if (_columns.Count == 0)
            return;
        var col = _columns[_selectedColumn];
        if (_selectedCard >= col.Cards.Count)
            return;

        // Capture the card being reordered by id — j/k below moves _selectedCard
        // to pick a target slot, so the moved card can no longer be found by
        // cursor position alone once the cursor has stepped onto a different card.
        _reorderMode = true;
        _reorderCardId = col.Cards[_selectedCard].Id;
        await RenderAsync();
    }

    private async Task ConfirmReorderAsync()
    {
        if (_columns.Count == 0 || _reorderCardId == null)
        {
            _reorderMode = false;
            _reorderCardId = null;
            return;
        }
        var col = _columns[_selectedColumn];
        var card = col.Cards.FirstOrDefault(c => c.Id == _reorderCardId.Value);
        if (card == null)
        {
            _reorderMode = false;
            _reorderCardId = null;
            return;
        }

        try
        {

            await Client.MoveAsync(
                _projectId,
                card.Id,
                new MoveCardRequest
                {
                    TargetColumnId = col.Id,
                    TargetPosition = _selectedCard,
                    ConfirmBlockedMove = false,
                    Version = card.Version,
                }
            );

            _reorderMode = false;
            _reorderCardId = null;
            await LoadBoardAsync();
            await RenderAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            _reorderMode = false;
            _reorderCardId = null;
            AnsiConsole.MarkupLine(
                "[yellow]Move blocked by dependencies. Use --force to override.[/]"
            );
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Reorder failed: {ex.Message}");
            _reorderMode = false;
            _reorderCardId = null;
            await RenderAsync();
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            _reorderMode = false;
            _reorderCardId = null;
            await RenderAsync();
        }
    }

    // Overlays pushed from here (DependencyPanel, ErrorPanelScreen, SpecViewerScreen via
    // CardDetailScreen) are entered by direct _appState.CurrentScreen assignment, bypassing
    // OnExitAsync — so this screen's handlers can still be subscribed while a different
    // screen is actually on-screen. Data still refreshes regardless; RenderAsync() is
    // guarded so a background event doesn't stomp whatever the user is really looking at.
    private async void HandleBoardEvent(SignalRConnectionManager.BoardEvent evt)
    {
        // Reload board data on any event for simplicity
        // (Future optimization: apply delta updates)
        await LoadBoardAsync();
        if (_appState.CurrentScreen == this)
            await RenderAsync();
    }

    private async void HandleCurrentUsers(List<SignalRConnectionManager.PresenceUser> users)
    {
        _appState.OnlineUsers.Clear();
        foreach (var user in users)
            _appState.OnlineUsers[user.UserId] = user.Username;
        _appState.OnlineCount = _appState.OnlineUsers.Count;
        if (_appState.CurrentScreen == this)
            await RenderAsync();
    }

    private async void HandleUserJoined(SignalRConnectionManager.PresenceUser user)
    {
        _appState.OnlineUsers[user.UserId] = user.Username;
        _appState.OnlineCount = _appState.OnlineUsers.Count;
        if (_appState.CurrentScreen == this)
            await RenderAsync();
    }

    private async void HandleUserLeft(SignalRConnectionManager.PresenceUser user)
    {
        _appState.OnlineUsers.Remove(user.UserId);
        _appState.OnlineCount = _appState.OnlineUsers.Count;
        if (_appState.CurrentScreen == this)
            await RenderAsync();
    }

    private async void HandleCardFocused(Guid userId, Guid cardId)
    {
        _appState.FocusedCards[userId] = cardId;
        if (_appState.CurrentScreen == this)
            await RenderAsync();
    }

    private async void HandleCardUnfocused(Guid userId)
    {
        _appState.FocusedCards.Remove(userId);
        if (_appState.CurrentScreen == this)
            await RenderAsync();
    }

    private async void HandleUnreadCountChanged(int count)
    {
        // count is authoritative from SignalRConnectionManager (already applied to
        // AppState.UnreadNotifications) — just trigger a re-render.
        if (_appState.CurrentScreen == this)
            await RenderAsync();
    }
}
