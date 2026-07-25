using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
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

    private List<BoardRenderer.ColumnData> _columns = new();
    private int _selectedColumn;
    private int _selectedCard;
    private string _projectName = "";
    private Guid _projectId;

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
    }

    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        AnsiConsole.Clear();

        var totalCards = _columns.Sum(c => c.Cards.Count);
        var layout = _renderer.BuildLayout(
            _columns, _selectedColumn, _selectedCard, _projectName, totalCards);

        AnsiConsole.Write(layout);

        AnsiConsole.MarkupLine("[grey][[h/l]] Columns  [[j/k]] Cards  [[Enter]] Detail  [[n]] New  [[m]] Move  [[?]] Help  [[Esc]] Back[/]");
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

            case ConsoleKey.Enter:
                await OpenCardDetailAsync();
                break;

            case ConsoleKey.N:
                await CreateCardAsync();
                break;

            case ConsoleKey.M:
                await MoveCardAsync();
                break;

            case ConsoleKey.Escape:
                var projectListScreen = new ProjectListScreen(
                    _apiClientFactory, _appState, _errorCollector, _connectionManager);
                _appState.CurrentScreen = projectListScreen;
                _appState.SelectedProjectId = null;
                await projectListScreen.OnEnterAsync();
                break;

            case ConsoleKey.D:
                // Dependency panel — Task 11
                break;

            case ConsoleKey.R:
                // Reorder — Task 10
                break;

            case ConsoleKey.Delete:
                await ArchiveCardAsync();
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
                            c.Type.ToString(), // Convert enum to string
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
        // Card detail screen wired in Task 9
        AnsiConsole.MarkupLine($"[green]Opening card #{card.CardNumber}...[/]");
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
}