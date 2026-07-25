using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Renderers;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class BoardScreen : IScreen
{
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private readonly ConnectionManager _connectionManager;
    private readonly BoardRenderer _renderer = new();
    private SignalRConnectionManager? _signalR;
    private readonly SemaphoreSlim _renderLock = new(1, 1);

    private List<BoardRenderer.ColumnData> _columns = new();
    private int _selectedColumn;
    private int _selectedCard;
    private string _projectName = "";
    private Guid _projectId;
    private bool _reorderMode = false;

    public BoardScreen(
        ApiClientFactory apiClientFactory,
        AppState appState,
        ErrorCollector errorCollector,
        ConnectionManager connectionManager)
    {
        _apiClientFactory = apiClientFactory;
        _appState = appState;
        _errorCollector = errorCollector;
        _connectionManager = connectionManager;
    }

    public async Task OnEnterAsync()
    {
        _projectId = _appState.SelectedProjectId ?? Guid.Empty;
        await LoadBoardAsync();

        // Connect SignalR — real-time sync degrades gracefully if the server is unreachable
        _signalR = new SignalRConnectionManager(_appState, _errorCollector);
        _signalR.OnBoardEvent += HandleBoardEvent;
        _signalR.OnCurrentUsers += async users =>
        {
            _appState.OnlineCount = users.Count;
            await RenderAsync();
        };
        _signalR.OnUserJoined += async _ =>
        {
            _appState.OnlineCount++;
            await RenderAsync();
        };
        _signalR.OnUserLeft += async _ =>
        {
            _appState.OnlineCount = Math.Max(0, _appState.OnlineCount - 1);
            await RenderAsync();
        };

        try
        {
            await _signalR.ConnectAsync(_projectId);
        }
        catch (Exception ex)
        {
            _appState.Connection = ConnectionStatus.Disconnected;
            _errorCollector.Add("N/A", $"Real-time connection failed: {ex.Message}");
        }
    }

    public async Task OnExitAsync()
    {
        if (_signalR != null)
            await _signalR.DisconnectAsync();
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
                _columns, _selectedColumn, _selectedCard, _projectName, totalCards,
                _appState.Connection, _appState.OnlineCount, _errorCollector.Count);

            AnsiConsole.Write(layout);

            KeyHintBar.Render(new[]
            {
                "[h/l] Columns", "[j/k] Cards", "[Enter] Detail", "[n] New",
                "[m] Move", "[?] Help", "[Esc] Back", "[q] Quit",
            });
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
                if (_selectedColumn > 0)
                {
                    _selectedColumn--;
                    _selectedCard = 0;
                }
                await RenderAsync();
                break;

            case ConsoleKey.L or ConsoleKey.RightArrow:
                if (_selectedColumn < _columns.Count - 1)
                {
                    _selectedColumn++;
                    _selectedCard = 0;
                }
                await RenderAsync();
                break;

            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (_columns.Count > 0)
                {
                    var col = _columns[_selectedColumn];
                    if (_selectedCard < col.Cards.Count - 1)
                        _selectedCard++;
                }
                await RenderAsync();
                break;

            case ConsoleKey.K or ConsoleKey.UpArrow:
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

            case ConsoleKey.Enter when _reorderMode:
                await ConfirmReorderAsync();
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

            case ConsoleKey.Escape when _reorderMode:
                _reorderMode = false;
                await RenderAsync();
                break;

            case ConsoleKey.Escape:
                if (_reorderMode)
                {
                    _reorderMode = false;
                    await RenderAsync();
                }
                else
                {
                    await OnExitAsync();
                    var projectListScreen = new ProjectListScreen(
                        _apiClientFactory, _appState, _errorCollector, _connectionManager);
                    _appState.CurrentScreen = projectListScreen;
                    _appState.SelectedProjectId = null;
                    await projectListScreen.OnEnterAsync();
                    await projectListScreen.RenderAsync();
                }
                break;

            case ConsoleKey.E:
                await EditCardTitleAsync();
                break;

            case ConsoleKey.R:
                await EnterReorderModeAsync();
                break;

            case ConsoleKey.N:
                await CreateCardAsync();
                break;

            case ConsoleKey.M:
                await MoveCardAsync();
                break;

            case ConsoleKey.D:
                // Dependency panel — Task 11
                break;

            case ConsoleKey.Delete:
                await ArchiveCardAsync();
                break;

            case ConsoleKey.Q:
                var confirm = AnsiConsole.Confirm("Quit HydraForge?");
                if (confirm)
                    Environment.Exit(0);
                break;
        }
    }

    private async Task LoadBoardAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();

            // Load project
            var project = await client.ProjectsGET2Async(_projectId);
            _projectName = project.Name;

            // Load cards
            var cardList = await client.CardsGETAsync(_projectId);
            var cards = cardList?.Cards ?? new List<CardResponse>();

            // Build column data
            _columns = project.Columns
                .OrderBy(c => c.Position)
                .Select(col => new BoardRenderer.ColumnData(
                    col.Id,
                    col.Name,
                    col.Position,
                    col.WipLimit,
                    col.Color,
                    cards
                        .Where(c => c.ColumnId == col.Id)
                        .OrderBy(c => c.Position)
                        .Select(c => new BoardRenderer.CardData(
                            c.Id,
                            c.CardNumber,
                            c.Title,
                            CardTypeMapper.ToDisplayString(c.Type), // Convert enum to string
                            false, // Blocked indicator — Task 12
                            c.Assignees?.Select(a => a.Username[..1].ToUpper()).ToList() ?? new(),
                            c.Version
                        ))
                        .ToList()
                ))
                .ToList();
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
        if (_columns.Count == 0) return;
        var col = _columns[_selectedColumn];
        if (_selectedCard >= col.Cards.Count) return;

        var card = col.Cards[_selectedCard];
        _appState.SelectedCardId = card.Id;

        await OnExitAsync();
        var detailScreen = new CardDetailScreen(_apiClientFactory, _appState, _errorCollector, _connectionManager);
        _appState.CurrentScreen = detailScreen;
        await detailScreen.OnEnterAsync();
        await detailScreen.RenderAsync();
    }

    private async Task CreateCardAsync()
    {
        if (_columns.Count == 0) return;
        var col = _columns[_selectedColumn];

        var title = AnsiConsole.Prompt(
            new TextPrompt<string>("Card title:")
                .Validate(t => string.IsNullOrWhiteSpace(t)
                    ? ValidationResult.Error("Title required")
                    : ValidationResult.Success()));

        var type = AnsiConsole.Prompt(
            new SelectionPrompt<HydraForge.Tui.Generated.CardType>()
                .Title("Type:")
                .AddChoices(HydraForge.Tui.Generated.CardType.Task, HydraForge.Tui.Generated.CardType.Issue, HydraForge.Tui.Generated.CardType.Goal, HydraForge.Tui.Generated.CardType.Idea));

        try
        {
            var client = _apiClientFactory.GetClient();

            await client.CardsPOSTAsync(_projectId, new CreateCardRequest
            {
                ColumnId = col.Id,
                Title = title,
                Description = "",
                Type = type,
                ParentCardId = null,
                DueAt = null,
                AssigneeUserIds = new List<Guid>()
            });

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
        if (_columns.Count == 0) return;
        var sourceCol = _columns[_selectedColumn];
        if (_selectedCard >= sourceCol.Cards.Count) return;
        var card = sourceCol.Cards[_selectedCard];

        var targetNames = _columns.Select(c => c.Name).ToList();
        var targetName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Move to column:")
                .AddChoices(targetNames));

        var targetCol = _columns.First(c => c.Name == targetName);

        try
        {
            var client = _apiClientFactory.GetClient();

            await client.MoveAsync(_projectId, card.Id, new MoveCardRequest
            {
                TargetColumnId = targetCol.Id,
                TargetPosition = targetCol.Cards.Count,
                ConfirmBlockedMove = false,
                Version = card.Version
            });

            await LoadBoardAsync();
            await RenderAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            AnsiConsole.MarkupLine("[yellow]Move blocked by dependencies. Use --force to override.[/]");
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
        if (_columns.Count == 0) return;
        var col = _columns[_selectedColumn];
        if (_selectedCard >= col.Cards.Count) return;
        var card = col.Cards[_selectedCard];

        var confirm = AnsiConsole.Confirm($"Archive card #{card.CardNumber}?");
        if (!confirm) return;

        try
        {
            var client = _apiClientFactory.GetClient();

            await client.ArchiveAsync(_projectId, card.Id, new ArchiveCardRequest { Version = card.Version });

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
        if (_columns.Count == 0) return;
        var col = _columns[_selectedColumn];
        if (_selectedCard >= col.Cards.Count) return;
        var card = col.Cards[_selectedCard];

        try
        {
            var client = _apiClientFactory.GetClient();

            var newTitle = AnsiConsole.Prompt(
                new TextPrompt<string>($"New title (current: {card.Title}):")
                    .DefaultValue(card.Title)
                    .Validate(t => string.IsNullOrWhiteSpace(t)
                        ? ValidationResult.Error("Title required")
                        : ValidationResult.Success()));

            await client.CardsPUTAsync(_projectId, card.Id, new UpdateCardRequest
            {
                Title = newTitle,
                Description = "",
                Type = CardTypeMapper.FromDisplayString(card.Type),
                ParentCardId = null,
                DueAt = null,
                Version = card.Version
            });

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
        _reorderMode = true;
        AnsiConsole.MarkupLine("[yellow]Reorder mode: Use j/k to navigate, Enter to confirm, Esc to cancel[/]");
        await RenderAsync();
    }

    private async Task ConfirmReorderAsync()
    {
        if (_columns.Count == 0) return;
        var col = _columns[_selectedColumn];
        if (_selectedCard >= col.Cards.Count) return;
        var card = col.Cards[_selectedCard];

        try
        {
            var client = _apiClientFactory.GetClient();

            await client.MoveAsync(_projectId, card.Id, new MoveCardRequest
            {
                TargetColumnId = col.Id,
                TargetPosition = _selectedCard,
                ConfirmBlockedMove = false,
                Version = card.Version
            });

            _reorderMode = false;
            await LoadBoardAsync();
            await RenderAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            AnsiConsole.MarkupLine("[yellow]Move blocked by dependencies. Use --force to override.[/]");
            _reorderMode = false;
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Reorder failed: {ex.Message}");
            _reorderMode = false;
            await RenderAsync();
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            _reorderMode = false;
            await RenderAsync();
        }
    }

    private async void HandleBoardEvent(SignalRConnectionManager.BoardEvent evt)
    {
        // Reload board data on any event for simplicity
        // (Future optimization: apply delta updates)
        await LoadBoardAsync();
        await RenderAsync();
    }
}