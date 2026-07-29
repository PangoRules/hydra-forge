using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class ProjectListScreen(
    ApiClientFactory apiClientFactory,
    AppState appState,
    ErrorCollector errorCollector,
    ConnectionManager connectionManager,
    SignalRConnectionManager signalRConnectionManager,
    NotificationCenter notificationCenter
) : IScreen
{
	private bool _signalRSubscribed;

	// Matches the Web UI's server-paginated project list convention
	// (docs/specs/2026-07-07-project-list-redesign-design.md): page size 20,
	// default sort CreatedAt descending.
	private const int PageSize = 20;

	private List<ProjectItem> _projects = [];
	private int _selectedIndex;
	private bool _showArchived;
	private string _searchFilter = "";
	private int _totalCount;
	private int _skip;
	private ProjectSortField _sortField = ProjectSortField.CreatedAt;
	private bool _sortDescending = true;
	private MemberRole? _roleFilter;
	private bool _notMemberFilter;

	private HydraForgeApiClient Client => apiClientFactory.GetClient();

	public async Task OnEnterAsync()
	{
		await LoadProjectsAsync();

		// NotificationHub is user-scoped and connects once here, the first screen after
		// login — idempotent, so re-entering the project list is a cheap no-op.
		try
		{
			await signalRConnectionManager.ConnectNotificationsAsync();
		}
		catch (Exception ex)
		{
			errorCollector.Add("N/A", $"Notification connection failed: {ex.Message}");
		}

		if (!_signalRSubscribed)
		{
			signalRConnectionManager.OnUnreadCountChanged += HandleUnreadCountChanged;
			_signalRSubscribed = true;
		}

		await notificationCenter.FetchUnreadCountAsync();
	}

	public Task OnExitAsync()
	{
		if (_signalRSubscribed)
		{
			signalRConnectionManager.OnUnreadCountChanged -= HandleUnreadCountChanged;
			_signalRSubscribed = false;
		}
		return Task.CompletedTask;
	}

	private async void HandleUnreadCountChanged(int count)
	{
		await RenderAsync();
	}

	public async Task RenderAsync()
	{
		AnsiConsole.Clear();

		// Header
		var header = new Rule("[blue]Projects[/]");
		AnsiConsole.Write(header);

		if (_searchFilter.Length > 0)
			AnsiConsole.MarkupLine($"[grey]Filter: \"{Markup.Escape(_searchFilter)}\"[/]");

		var roleLabel =
		    _notMemberFilter ? "Not member"
		    : _roleFilter.HasValue ? GetRoleString(_roleFilter.Value)
		    : "All";
		var sortArrow = _sortDescending ? "↓" : "↑";
		AnsiConsole.MarkupLine($"[grey]Sort: {_sortField} {sortArrow}   Role: {roleLabel}[/]");

		// Table — Table.Expand() distributes leftover width EQUALLY across every
		// column (including the narrow fixed ones), which just moves the blank
		// gap inside each column instead of removing it. So instead we size
		// "Name" explicitly to soak up whatever the terminal doesn't need for
		// the other (inherently narrow) columns.
		const int otherColumnsWidth = 3 + 9 + 8 + 10 + 10; // #, Members, Role, Created, Archived
		const int columnCount = 6;
		const int cellPadding = 2; // left+right padding per cell
		var chrome = columnCount * cellPadding + columnCount + 1; // padding + border/separator chars
		var nameWidth = Math.Max(20, AnsiConsole.Profile.Width - otherColumnsWidth - chrome);

		var table = new Table()
		    .Border(TableBorder.Rounded)
		    .AddColumn(new TableColumn("#").Centered().Width(3))
		    .AddColumn(new TableColumn("Name").Width(nameWidth))
		    .AddColumn(new TableColumn("Members").Centered().Width(9))
		    .AddColumn(new TableColumn("Role").Width(8))
		    .AddColumn(new TableColumn("Created").Width(10))
		    .AddColumn(new TableColumn("Archived").Centered().Width(10));

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
			    p.ArchivedAt != null ? "[grey]Yes[/]" : ""
			);
		}

		AnsiConsole.Write(table);

		// Footer — sits directly under the table, no blank line in between.
		var totalPages = _totalCount == 0 ? 1 : (int)Math.Ceiling(_totalCount / (double)PageSize);
		var currentPage = _skip / PageSize + 1;
		var rangeStart = _totalCount == 0 ? 0 : _skip + 1;
		var rangeEnd = Math.Min(_skip + _projects.Count, _totalCount);
		AnsiConsole.MarkupLine(
		    $"[grey]Showing {rangeStart}-{rangeEnd} of {_totalCount} projects (page {currentPage}/{totalPages})    |    {appState.UnreadNotifications} unread[/]"
		);

		var errors = errorCollector.GetErrors();
		var hints = new List<string>
	{
	    "[Enter] Open",
	    "[c] Create",
	    "[/] Search",
	    "[a] Archived",
	    "[u] Notifications",
	    "[q] Quit",
	    "[s] Sort",
	    "[Shift+S] Direction",
	    "[r] Role filter",
	    "[n]/[p] Page",
	    "[?] Help",
	};
		if (errors.Count > 0)
			hints.Add("[x] Dismiss errors");
		KeyHintBar.Render(hints);

		RenderErrors(errors);
	}

	// Nothing gets swallowed silently — every caught ApiException/HttpRequestException
	// in this screen goes through _errorCollector, and this is where it surfaces.
	private static void RenderErrors(
	    IReadOnlyList<(DateTime Timestamp, string CorrelationId, string Message)> errors
	)
	{
		if (errors.Count == 0)
			return;

		AnsiConsole.WriteLine();
		var panel = new Panel(
		    new Rows(
			errors
			    .TakeLast(3)
			    .Select(e => new Markup(
				$"[red]⚠ {Markup.Escape(e.Message)}[/] [grey]({e.Timestamp:HH:mm:ss}, correlationId: {Markup.Escape(e.CorrelationId)})[/]"
			    ))
		    )
		)
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
			case ConsoleKey.N when key.Modifiers == ConsoleModifiers.Control:
				if (_selectedIndex < _projects.Count - 1)
					_selectedIndex++;
				await RenderAsync();
				break;

			case ConsoleKey.K
		    or ConsoleKey.UpArrow:
			case ConsoleKey.P when key.Modifiers == ConsoleModifiers.Control:
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
					appState.SelectedProjectId = selected.Id;

					await OnExitAsync();
					var boardScreen = new BoardScreen(
					    apiClientFactory,
					    appState,
					    errorCollector,
					    connectionManager,
					    signalRConnectionManager,
					    notificationCenter
					);
					appState.CurrentScreen = boardScreen;
					await boardScreen.OnEnterAsync();
					await boardScreen.RenderAsync();
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

			case ConsoleKey.Divide
		    or ConsoleKey.Oem2: // '/' key
				_searchFilter = AnsiConsole.Prompt(
				    new TextPrompt<string>("Search:").DefaultValue(_searchFilter).AllowEmpty()
				);
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
				if (_notMemberFilter)
				{
					_notMemberFilter = false;
					_roleFilter = null;
				}
				else if (_roleFilter is null)
					_roleFilter = MemberRole.Owner;
				else if (_roleFilter == MemberRole.Owner)
					_roleFilter = MemberRole.Member;
				else
				{
					_notMemberFilter = true;
					_roleFilter = null;
				}
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
				while (errorCollector.Count > 0)
					errorCollector.Dismiss(0);
				await RenderAsync();
				break;

			case ConsoleKey.U:
				await notificationCenter.ShowNotificationsAsync(RenderAsync);
				break;

			case ConsoleKey.Q:
				var confirm = AnsiConsole.Confirm("Quit HydraForge?");
				if (confirm)
					Environment.Exit(0);
				break;

			case ConsoleKey k when key.KeyChar == '?':
				ShowHelp();
				await RenderAsync();
				break;
		}
	}

	private static void ShowHelp() =>
	    HelpOverlay.Show(
		"Projects",
		[
		("j/k, ↑/↓, Ctrl+n/p", "Prev/next row"),
		("g/Home, G/End", "First / last row"),
		("Enter", "Open board"),
		("c", "Create project"),
		("/", "Search"),
		("a", "Toggle archived"),
		("s", "Cycle sort field"),
		("Shift+S", "Toggle sort direction"),
		("r", "Cycle role filter"),
		("n / p", "Next / prev page"),
		("u", "Notifications"),
		("x", "Dismiss errors"),
		("q", "Quit"),
		("?", "This help"),
		]
	    );

	private async Task LoadProjectsAsync()
	{
		try
		{
			// NSwag generates ProjectsGETAsync with optional parameters
			var page = await Client.ProjectsGETAsync(
			    includeArchived: _showArchived,
			    search: string.IsNullOrEmpty(_searchFilter) ? null : _searchFilter,
			    sortBy: _sortField,
			    sortDescending: _sortDescending,
			    role: _roleFilter,
			    skip: _skip,
			    take: PageSize,
			    excludeMembership: _notMemberFilter ? true : null
			);

			if (page != null)
			{
				_projects = [.. page
				    .Items.Select(p => new ProjectItem(
					p.Id,
					p.Name,
					p.CreatedAt,
					p.ArchivedAt,
					p.MemberCount,
					p.MyRole
				    ))];
				_totalCount = page.TotalCount;
				if (_selectedIndex >= _projects.Count)
					_selectedIndex = Math.Max(0, _projects.Count - 1);
			}
		}
		catch (ApiException ex)
		{
			errorCollector.Add("N/A", $"Failed to load projects ({ex.StatusCode}): {ex.Message}");
		}
		catch (HttpRequestException ex)
		{
			errorCollector.Add("N/A", $"Connection error: {ex.Message}");
			var connected = await connectionManager.WaitForConnectionAsync();
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
		    new TextPrompt<string>("Project name:").Validate(n =>
			string.IsNullOrWhiteSpace(n)
			    ? ValidationResult.Error("Name required")
			    : ValidationResult.Success()
		    )
		);

		var description = AnsiConsole.Prompt(
		    new TextPrompt<string>("Description (optional):").AllowEmpty()
		);

		var template = AnsiConsole.Prompt(
		    new SelectionPrompt<string>()
			.Title("Template:")
			.AddChoices("General", "Software", "Blank")
		);

		try
		{
			await Client.ProjectsPOSTAsync(
			    new CreateProjectRequest
			    {
				    Name = name,
				    Description = description,
				    Template = template switch
				    {
					    "Software" => ColumnTemplate.Software,
					    "Blank" => ColumnTemplate.Blank,
					    _ => ColumnTemplate.General,
				    },
			    }
			);

			AnsiConsole.MarkupLine($"[green]Project \"{Markup.Escape(name)}\" created![/]");
			await LoadProjectsAsync();
			await RenderAsync();
		}
		catch (ApiException ex)
		{
			errorCollector.Add("N/A", $"Create project failed: {ex.Message}");
		}
		catch (HttpRequestException ex)
		{
			errorCollector.Add("N/A", $"Connection error: {ex.Message}");
		}
	}

	private static string FormatRelative(DateTimeOffset dt)
	{
		var diff = DateTimeOffset.UtcNow - dt;
		if (diff.TotalDays > 365)
			return $"{(int)(diff.TotalDays / 365)}y ago";
		if (diff.TotalDays > 30)
			return $"{(int)(diff.TotalDays / 30)}mo ago";
		if (diff.TotalDays >= 1)
			return $"{(int)diff.TotalDays}d ago";
		if (diff.TotalHours >= 1)
			return $"{(int)diff.TotalHours}h ago";
		return "just now";
	}

	private static string GetRoleString(MemberRole? role) => role?.ToString() ?? "—";

	// Local view model — maps from NSwag-generated DTOs
	private record ProjectItem(
	    Guid Id,
	    string Name,
	    DateTimeOffset CreatedAt,
	    DateTimeOffset? ArchivedAt,
	    int MemberCount,
	    MemberRole? MyRole
	);
}
