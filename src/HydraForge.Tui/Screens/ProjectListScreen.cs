using HydraForge.Tui.Generated;
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

    // Matches the Web UI's server-paginated project list convention
    // (docs/specs/2026-07-07-project-list-redesign-design.md): page size 20,
    // default sort CreatedAt descending.
    private const int PageSize = 20;

    private List<ProjectItem> _projects = new();
    private int _selectedIndex;
    private bool _showArchived;
    private string _searchFilter = "";
    private int _totalCount;
    private int _skip;
    private ProjectSortField _sortField = ProjectSortField.CreatedAt;
    private bool _sortDescending = true;
    private MemberRole? _roleFilter;

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
        var header = new Rule("[blue]Projects[/]");
        AnsiConsole.Write(header);

        if (_searchFilter.Length > 0)
            AnsiConsole.MarkupLine($"[grey]Filter: \"{Markup.Escape(_searchFilter)}\"[/]");

        var roleLabel = _roleFilter.HasValue ? GetRoleString(_roleFilter.Value) : "All";
        var sortArrow = _sortDescending ? "↓" : "↑";
        AnsiConsole.MarkupLine($"[grey]Sort: {_sortField} {sortArrow}   Role: {roleLabel}[/]");

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
                GetRoleString(p.MyRole),
                FormatRelative(p.CreatedAt),
                p.ArchivedAt != null ? "[grey]A[/]" : ""
            );
        }

        AnsiConsole.Write(table);

        // Footer
        AnsiConsole.WriteLine();
        var totalPages = _totalCount == 0 ? 1 : (int)Math.Ceiling(_totalCount / (double)PageSize);
        var currentPage = _skip / PageSize + 1;
        var rangeStart = _totalCount == 0 ? 0 : _skip + 1;
        var rangeEnd = Math.Min(_skip + _projects.Count, _totalCount);
        AnsiConsole.MarkupLine($"[grey]Showing {rangeStart}-{rangeEnd} of {_totalCount} projects (page {currentPage}/{totalPages})[/]");

        AnsiConsole.MarkupLine("[grey][[Enter]] Open  [[c]] Create  [[/]] Search  [[a]] Archived  [[q]] Quit[/]");

        var errors = _errorCollector.GetErrors();
        var footerHint = errors.Count > 0
            ? "[grey][[s]] Sort  [[Shift+S]] Direction  [[r]] Role filter  [[n]]/[[p]] Page  [[x]] Dismiss errors[/]"
            : "[grey][[s]] Sort  [[Shift+S]] Direction  [[r]] Role filter  [[n]]/[[p]] Page[/]";
        AnsiConsole.MarkupLine(footerHint);

        RenderErrors(errors);
    }

    // Nothing gets swallowed silently — every caught ApiException/HttpRequestException
    // in this screen goes through _errorCollector, and this is where it surfaces.
    private static void RenderErrors(IReadOnlyList<(DateTime Timestamp, string CorrelationId, string Message)> errors)
    {
        if (errors.Count == 0)
            return;

        AnsiConsole.WriteLine();
        var panel = new Panel(
            new Rows(errors.TakeLast(3).Select(e =>
                new Markup($"[red]⚠ {Markup.Escape(e.Message)}[/] [grey]({e.Timestamp:HH:mm:ss}, correlationId: {Markup.Escape(e.CorrelationId)})[/]"))))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Red),
            Header = new PanelHeader($" Errors ({errors.Count}) "),
        };
        AnsiConsole.Write(panel);
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

            case ConsoleKey.G when key.Modifiers == ConsoleModifiers.Shift:
                _selectedIndex = Math.Max(0, _projects.Count - 1);
                await RenderAsync();
                break;

            case ConsoleKey.G:
                _selectedIndex = 0;
                await RenderAsync();
                break;

            case ConsoleKey.Home:
                _selectedIndex = 0;
                await RenderAsync();
                break;

            case ConsoleKey.End:
                _selectedIndex = Math.Max(0, _projects.Count - 1);
                await RenderAsync();
                break;

            case ConsoleKey.Enter:
                if (_projects.Count > 0)
                {
                    var selected = _projects[_selectedIndex];
                    _appState.SelectedProjectId = selected.Id;
                    // Board screen will be wired in Task 7
                    AnsiConsole.MarkupLine($"[green]Opening project: {Markup.Escape(selected.Name)}[/]");
                }
                break;

            case ConsoleKey.C:
                await CreateProjectAsync();
                break;

            case ConsoleKey.A:
                _showArchived = !_showArchived;
                _skip = 0;
                _selectedIndex = 0;
                await LoadProjectsAsync();
                await RenderAsync();
                break;

            case ConsoleKey.Divide or ConsoleKey.Oem2: // '/' key
                _searchFilter = AnsiConsole.Prompt(
                    new TextPrompt<string>("Search:")
                        .DefaultValue(_searchFilter)
                        .AllowEmpty());
                _skip = 0;
                _selectedIndex = 0;
                await LoadProjectsAsync();
                await RenderAsync();
                break;

            case ConsoleKey.S when key.Modifiers == ConsoleModifiers.Shift:
                _sortDescending = !_sortDescending;
                _skip = 0;
                _selectedIndex = 0;
                await LoadProjectsAsync();
                await RenderAsync();
                break;

            case ConsoleKey.S:
                _sortField = (ProjectSortField)(((int)_sortField + 1) % 3);
                _skip = 0;
                _selectedIndex = 0;
                await LoadProjectsAsync();
                await RenderAsync();
                break;

            case ConsoleKey.R:
                _roleFilter = _roleFilter switch
                {
                    null => MemberRole.Owner,
                    MemberRole.Owner => MemberRole.Member,
                    _ => null
                };
                _skip = 0;
                _selectedIndex = 0;
                await LoadProjectsAsync();
                await RenderAsync();
                break;

            case ConsoleKey.N:
                if (_skip + PageSize < _totalCount)
                {
                    _skip += PageSize;
                    _selectedIndex = 0;
                    await LoadProjectsAsync();
                    await RenderAsync();
                }
                break;

            case ConsoleKey.P:
                if (_skip > 0)
                {
                    _skip = Math.Max(0, _skip - PageSize);
                    _selectedIndex = 0;
                    await LoadProjectsAsync();
                    await RenderAsync();
                }
                break;

            case ConsoleKey.X:
                while (_errorCollector.Count > 0)
                    _errorCollector.Dismiss(0);
                await RenderAsync();
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

            // NSwag generates ProjectsGETAsync with optional parameters
            var page = await client.ProjectsGETAsync(
                includeArchived: _showArchived,
                search: string.IsNullOrEmpty(_searchFilter) ? null : _searchFilter,
                sortBy: _sortField,
                sortDescending: _sortDescending,
                role: _roleFilter,
                skip: _skip,
                take: PageSize);

            if (page != null)
            {
                _projects = page.Items
                    .Select(p => new ProjectItem(
                        p.Id, p.Name, p.CreatedAt, p.ArchivedAt,
                        p.MemberCount, p.MyRole))
                    .ToList();
                _totalCount = page.TotalCount;
                if (_selectedIndex >= _projects.Count)
                    _selectedIndex = Math.Max(0, _projects.Count - 1);
            }
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load projects ({ex.StatusCode}): {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            var connected = await _connectionManager.WaitForConnectionAsync();
            if (connected)
            {
                await LoadProjectsAsync();
                await RenderAsync();
            }
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

            await client.ProjectsPOSTAsync(new HydraForge.Tui.Generated.CreateProjectRequest
            {
                Name = name,
                Description = description,
                Template = template switch
                {
                    "Software" => ColumnTemplate.Software,
                    "Blank" => ColumnTemplate.Blank,
                    _ => ColumnTemplate.General
                }
            });

            AnsiConsole.MarkupLine($"[green]Project \"{Markup.Escape(name)}\" created![/]");
            await LoadProjectsAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Create project failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private static string FormatRelative(DateTimeOffset dt)
    {
        var diff = DateTimeOffset.UtcNow - dt;
        if (diff.TotalDays > 365) return $"{(int)(diff.TotalDays / 365)}y ago";
        if (diff.TotalDays > 30) return $"{(int)(diff.TotalDays / 30)}mo ago";
        if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
        return "just now";
    }

    private static string GetRoleString(MemberRole role) => role.ToString();

    // Local view model — maps from NSwag-generated DTOs
    private record ProjectItem(
        Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? ArchivedAt,
        int MemberCount, MemberRole MyRole
    );
}