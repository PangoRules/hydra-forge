# Plan 6: Project List View + Create Project

**Branch:** `task/tui-project-list`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 6

**Goal:** Full-screen project list table with keyboard nav, create project prompt workflow, filter/search.

**Depends on:** Task 3 (ApiClientFactory), Task 4 (auth flow in Program.cs).

---

## Step 1: Create `ProjectListScreen`

Create `src/HydraForge.Tui/Screens/ProjectListScreen.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class ProjectListScreen : IScreen
{
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private readonly ConnectionManager _connectionManager;

    private List<ProjectItem> _projects = new();
    private int _selectedIndex;
    private bool _showArchived;
    private string _searchFilter = "";
    private int _totalCount;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProjectListScreen(
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
        await LoadProjectsAsync();
    }

    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        AnsiConsole.Clear();

        // Header
        var header = new Rule("[blue]Projects[/]").LeftAligned();
        AnsiConsole.Write(header);

        if (_searchFilter.Length > 0)
            AnsiConsole.MarkupLine($"[grey]Filter: \"{_searchFilter}\"[/]");

        // Table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("#").Centered())
            .AddColumn("Name")
            .AddColumn(new TableColumn("Members").Centered())
            .AddColumn("Role")
            .AddColumn("Created")
            .AddColumn("");

        for (int i = 0; i < _projects.Count; i++)
        {
            var p = _projects[i];
            var isSelected = i == _selectedIndex;

            var nameMarkup = isSelected
                ? $"[blue bold]{Markup.Escape(p.Name)}[/]"
                : Markup.Escape(p.Name);

            var archivedBadge = p.ArchivedAt != null ? " [grey](archived)[/]" : "";

            table.AddRow(
                isSelected ? "[blue]>[/]" : " ",
                $"{nameMarkup}{archivedBadge}",
                p.MemberCount.ToString(),
                p.MyRole,
                FormatRelative(p.CreatedAt),
                p.ArchivedAt != null ? "[grey]A[/]" : ""
            );
        }

        AnsiConsole.Write(table);

        // Footer
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]{_totalCount} projects total[/]");
        AnsiConsole.MarkupLine("[grey][[Enter]] Open  [[c]] Create  [[/]] Search  [[a]] Toggle Archived  [[q]] Quit[/]");
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (_selectedIndex < _projects.Count - 1)
                    _selectedIndex++;
                await RenderAsync();
                break;

            case ConsoleKey.K or ConsoleKey.UpArrow:
                if (_selectedIndex > 0)
                    _selectedIndex--;
                await RenderAsync();
                break;

            case ConsoleKey.G or ConsoleKey.Home:
                _selectedIndex = 0;
                await RenderAsync();
                break;

            case ConsoleKey.End when key.Modifiers == ConsoleModifiers.Shift:
            case ConsoleKey.G when key.Modifiers == ConsoleModifiers.Shift:
                _selectedIndex = Math.Max(0, _projects.Count - 1);
                await RenderAsync();
                break;

            case ConsoleKey.Enter:
                if (_projects.Count > 0)
                {
                    var selected = _projects[_selectedIndex];
                    _appState.SelectedProjectId = selected.Id;
                    // Board screen will be wired in Task 7
                    AnsiConsole.MarkupLine($"[green]Opening project: {selected.Name}[/]");
                }
                break;

            case ConsoleKey.C:
                await CreateProjectAsync();
                break;

            case ConsoleKey.A:
                _showArchived = !_showArchived;
                await LoadProjectsAsync();
                break;

            case ConsoleKey.Divide or ConsoleKey.Oem2: // '/' key
                _searchFilter = AnsiConsole.Prompt(
                    new TextPrompt<string>("Search:")
                        .DefaultValue(_searchFilter)
                        .AllowEmpty());
                await LoadProjectsAsync();
                break;

            case ConsoleKey.Q:
                var confirm = AnsiConsole.Confirm("Quit HydraForge?");
                if (confirm)
                    Environment.Exit(0);
                break;
        }
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var url = $"api/projects?includeArchived={_showArchived.ToString().ToLower()}&take=50";
            if (!string.IsNullOrEmpty(_searchFilter))
                url += $"&search={Uri.EscapeDataString(_searchFilter)}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _errorCollector.Add("N/A", $"Failed to load projects: {response.StatusCode}");
                return;
            }

            var page = await response.Content.ReadFromJsonAsync<ProjectListPage>(JsonOptions);
            if (page != null)
            {
                _projects = page.Items;
                _totalCount = page.TotalCount;
                if (_selectedIndex >= _projects.Count)
                    _selectedIndex = Math.Max(0, _projects.Count - 1);
            }
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            await _connectionManager.WaitForConnectionAsync();
        }
    }

    private async Task CreateProjectAsync()
    {
        var name = AnsiConsole.Prompt(
            new TextPrompt<string>("Project name:")
                .Validate(n => string.IsNullOrWhiteSpace(n)
                    ? ValidationResult.Error("Name required")
                    : ValidationResult.Success()));

        var description = AnsiConsole.Prompt(
            new TextPrompt<string>("Description (optional):")
                .AllowEmpty());

        var template = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Template:")
                .AddChoices("General", "Software", "Blank"));

        try
        {
            var client = _apiClientFactory.GetClient();
            var payload = new
            {
                name,
                description,
                template = template switch
                {
                    "Software" => "Software",
                    "Blank" => "Blank",
                    _ => "General"
                }
            };

            var response = await client.PostAsJsonAsync("api/projects", payload, JsonOptions);
            if (response.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine($"[green]Project \"{name}\" created![/]");
                await LoadProjectsAsync();
                await RenderAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                AnsiConsole.MarkupLine($"[red]Failed: {response.StatusCode}[/]");
                _errorCollector.Add("N/A", $"Create project failed: {error}");
            }
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private static string FormatRelative(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        if (diff.TotalDays > 365) return $"{(int)(diff.TotalDays / 365)}y ago";
        if (diff.TotalDays > 30) return $"{(int)(diff.TotalDays / 30)}mo ago";
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        return "just now";
    }

    private record ProjectItem(
        Guid Id,
        string Name,
        string? Description,
        DateTime CreatedAt,
        DateTime? ArchivedAt,
        int MemberCount,
        string MyRole
    );

    private record ProjectListPage(List<ProjectItem> Items, int TotalCount);
}
```

## Step 2: Wire `ProjectListScreen` into `Program.cs`

Update `src/HydraForge.Tui/Program.cs` — replace the placeholder at the end:

```csharp
// ... (keep existing auth flow code) ...

// Initialize API client with valid token
apiClientFactory.CreateClient();

var connectionManager = new ConnectionManager(appState, apiClientFactory, errorCollector);

// Show project list
var projectListScreen = new ProjectListScreen(
    apiClientFactory, appState, errorCollector, connectionManager);
appState.CurrentScreen = projectListScreen;
await projectListScreen.OnEnterAsync();
await projectListScreen.RenderAsync();

// Main input loop
while (true)
{
    var key = Console.ReadKey(true);

    // Global shortcuts
    if (key.Key == ConsoleKey.Q && (key.Modifiers & ConsoleModifiers.Control) != 0)
    {
        Environment.Exit(0);
    }

    if (appState.CurrentScreen != null)
    {
        await appState.CurrentScreen.HandleKeyAsync(key);
    }
}

return 0;
```

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Screens/ProjectListScreen.cs src/HydraForge.Tui/Program.cs
git commit -m "feat(tui): add project list view with create, search, and keyboard nav"
```