# Plan 7: Board View — Columns + Cards + LiveDisplay

**Branch:** `task/tui-board-view`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 7

**Goal:** Board view with Spectre.Console column/card layout, keyboard navigation (h/l/j/k), LiveDisplay for real-time updates.

**Depends on:** Task 3 (ApiClientFactory), Task 4 (auth), Task 5 (ConnectionManager).

---

## Step 1: Create `BoardRenderer`

Create `src/HydraForge.Tui/Renderers/BoardRenderer.cs`:

```csharp
using Spectre.Console;

namespace HydraForge.Tui.Renderers;

public class BoardRenderer
{
    public record ColumnData(
        Guid Id,
        string Name,
        int Position,
        int? WipLimit,
        string? Color,
        List<CardData> Cards
    );

    public record CardData(
        Guid Id,
        int CardNumber,
        string Title,
        string Type,
        bool IsBlocked,
        List<string> AssigneeInitials,
        int Version
    );

    public Layout BuildLayout(
        List<ColumnData> columns,
        int selectedColumn,
        int selectedCard,
        string projectName,
        int totalCards)
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Title"),
                new Layout("Board"),
                new Layout("Status")
            );

        // Title bar
        layout["Title"].Update(
            new Panel(
                new Markup($"[blue bold]{Markup.Escape(projectName)}[/]  " +
                           $"[grey]{columns.Count} columns  {totalCards} cards[/]")
            ).Expand()
        );

        // Board area — split into columns
        var columnLayouts = columns.Select((col, i) =>
        {
            var isSelected = i == selectedColumn;
            var color = ParseColor(col.Color) ?? Color.Grey;
            var borderColor = isSelected ? Color.Blue : color;

            var cardPanels = col.Cards.Select((card, j) =>
            {
                var isCardSelected = isSelected && j == selectedCard;
                var prefix = card.IsBlocked ? "🔴 " : "";
                var typeBadge = card.Type switch
                {
                    "Task" => "[cyan1]T[/]",
                    "Issue" => "[red]I[/]",
                    "Goal" => "[yellow]G[/]",
                    "Idea" => "[green]D[/]",
                    _ => "[grey]?[/]"
                };

                var assignees = card.AssigneeInitials.Count > 0
                    ? " " + string.Join("", card.AssigneeInitials.Select(a => $"[grey]{a}[/]"))
                    : "";

                var title = card.Title.Length > 25
                    ? card.Title[..22] + "..."
                    : card.Title;

                var cardMarkup = $"{prefix}{typeBadge} #{card.CardNumber} {Markup.Escape(title)}{assignees}";

                return new Panel(new Markup(cardMarkup))
                {
                    Border = isCardSelected ? BoxBorder.Double : BoxBorder.None,
                    BorderColor = isCardSelected ? Color.Blue : null,
                };
            }).ToList();

            var wipText = col.WipLimit.HasValue
                ? $" ({col.Cards.Count}/{col.WipLimit})"
                : $" ({col.Cards.Count})";

            var header = new Panel(
                new Markup($"[{color.ToMarkup()} bold]{Markup.Escape(col.Name)}[/]{wipText}")
            )
            {
                Border = BoxBorder.Rounded,
                BorderColor = borderColor,
            };

            var content = new Rows(new List<IRenderable>(cardPanels));
            return new Panel(content)
            {
                Header = new PanelHeader($" {col.Name} "),
                Border = BoxBorder.Rounded,
                BorderColor = borderColor,
                Expand = true,
            };
        }).ToList();

        var columnsRow = new Columns(columnLayouts.Cast<IRenderable>().ToList());
        layout["Board"].Update(columnsRow);

        // Status bar placeholder (full impl in Task 17)
        layout["Status"].Update(
            new Panel(
                new Markup("[grey]● Connected    |    0 online    |    0 errors[/]")
            ).Expand()
        );

        return layout;
    }

    private static Color? ParseColor(string? colorName) => colorName?.ToLower() switch
    {
        "red" => Color.Red,
        "green" => Color.Green,
        "blue" => Color.Blue,
        "yellow" => Color.Yellow,
        "purple" => Color.Purple,
        "orange" => Color.Orange1,
        "cyan" => Color.Cyan1,
        _ => null
    };
}
```

## Step 2: Create `BoardScreen`

Create `src/HydraForge.Tui/Screens/BoardScreen.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

            case ConsoleKey.G:
                _selectedColumn = 0;
                _selectedCard = 0;
                await RenderAsync();
                break;

            case ConsoleKey.G when key.Modifiers == ConsoleModifiers.Shift:
                _selectedColumn = Math.Max(0, _columns.Count - 1);
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
                _appState.CurrentScreen = null; // Return to project list
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
            var projectResponse = await client.GetAsync($"api/projects/{_projectId}");
            if (!projectResponse.IsSuccessStatusCode) return;

            var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDetail>(JsonOptions);
            if (project == null) return;

            _projectName = project.Name;

            // Load cards
            var cardsResponse = await client.GetAsync($"api/projects/{_projectId}/cards");
            if (!cardsResponse.IsSuccessStatusCode) return;

            var cardList = await cardsResponse.Content.ReadFromJsonAsync<CardList>(JsonOptions);
            var cards = cardList?.Cards ?? new();

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
                            c.Type,
                            false, // Blocked indicator — Task 12
                            c.Assignees.Select(a => a.Username[..1].ToUpper()).ToList(),
                            c.Version
                        ))
                        .ToList()
                ))
                .ToList();
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
            new SelectionPrompt<string>()
                .Title("Type:")
                .AddChoices("Task", "Issue", "Goal", "Idea"));

        try
        {
            var client = _apiClientFactory.GetClient();
            var payload = new
            {
                columnId = col.Id,
                title,
                description = "",
                type,
                parentCardId = (Guid?)null,
                dueAt = (DateTime?)null,
                assigneeUserIds = Array.Empty<Guid>()
            };

            var response = await client.PostAsJsonAsync(
                $"api/projects/{_projectId}/cards", payload, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                await LoadBoardAsync();
                await RenderAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _errorCollector.Add("N/A", $"Create card failed: {error}");
            }
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
            var payload = new
            {
                targetColumnId = targetCol.Id,
                targetPosition = targetCol.Cards.Count,
                confirmBlockedMove = false,
                version = card.Version
            };

            var response = await client.PostAsJsonAsync(
                $"api/projects/{_projectId}/cards/{card.Id}/move", payload, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                await LoadBoardAsync();
                await RenderAsync();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                AnsiConsole.MarkupLine("[yellow]Move blocked by dependencies. Use --force to override.[/]");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _errorCollector.Add("N/A", $"Move failed: {error}");
            }
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
            var payload = new { version = card.Version };

            var response = await client.PostAsJsonAsync(
                $"api/projects/{_projectId}/cards/{card.Id}/archive", payload, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                await LoadBoardAsync();
                await RenderAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Archive error: {ex.Message}");
        }
    }

    // DTOs
    private record ProjectDetail(
        Guid Id, string Name, List<ColumnInfo> Columns
    );
    private record ColumnInfo(Guid Id, string Name, int Position, int? WipLimit, string? Color);
    private record CardList(List<CardInfo> Cards);
    private record CardInfo(
        Guid Id, Guid ColumnId, int CardNumber, string Title, string Type,
        int Position, int Version, List<AssigneeInfo> Assignees
    );
    private record AssigneeInfo(Guid UserId, string Username);
}
```

## Step 3: Wire `BoardScreen` into `ProjectListScreen`

Update `ProjectListScreen.HandleKeyAsync` — replace the `Enter` handler placeholder:

```csharp
case ConsoleKey.Enter:
    if (_projects.Count > 0)
    {
        var selected = _projects[_selectedIndex];
        _appState.SelectedProjectId = selected.Id;

        var boardScreen = new BoardScreen(
            _apiClientFactory, _appState, _errorCollector, _connectionManager);
        _appState.CurrentScreen = boardScreen;
        await boardScreen.OnEnterAsync();
        await boardScreen.RenderAsync();
    }
    break;
```

## Step 4: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 5: Commit

```bash
git add src/HydraForge.Tui/Renderers/BoardRenderer.cs src/HydraForge.Tui/Screens/BoardScreen.cs src/HydraForge.Tui/Screens/ProjectListScreen.cs
git commit -m "feat(tui): add board view with column/card layout and keyboard navigation"
```