using System.Text.Json;
using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace HydraForge.Tui.Screens;

public class CardDetailScreen(
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
    private readonly EditorLauncher _editorLauncher = new();

    private CardResponse? _card;
    private List<ChecklistItemResponse> _checklist = [];
    private List<CommentResponse> _comments = [];
    private List<CardRelationshipDto> _relationships = [];
    private Guid _projectId;
    private Guid _cardId;
    private int _sectionIndex;
    private int _checklistIndex;
    private int _dependencyIndex;

    public async Task OnEnterAsync()
    {
        _projectId = _appState.SelectedProjectId ?? Guid.Empty;
        _cardId = _appState.SelectedCardId ?? Guid.Empty;
        await LoadCardAsync();
    }

    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        if (_card == null)
            return;

        AnsiConsole.Clear();

        // Header
        var typeColor = GetTypeColor(_card.Type);
        AnsiConsole.Write(
            new Rule(
                $"[{typeColor}]#{_card.CardNumber}[/] [blue bold]{Markup.Escape(_card.Title)}[/]"
            )
        );

        // Sections — build content first, then wrap in panels
        var content = new List<IRenderable>
        {
            BuildMetadataPanel(),
            BuildDescriptionPanel(),
            BuildChecklistPanel(),
            BuildCommentsPanel(),
            BuildDependenciesPanel(),
        };

        // Highlight active section
        if (_sectionIndex < content.Count)
        {
            var active = BuildSectionPanel(_sectionIndex, isActive: true);
            content[_sectionIndex] = active;
        }

        AnsiConsole.Write(new Rows(content));
        AnsiConsole.WriteLine();
        KeyHintBar.Render(BuildHints());
    }

    // Hints reflect what the current section actually does — e.g. [j/k] only
    // means something inside the checklist, [e] only edits title/description.
    private IEnumerable<string> BuildHints()
    {
        yield return "[Tab] Sections";

        switch (_sectionIndex)
        {
            case 0:
                yield return "[e] Edit field";
                break;
            case 1:
                yield return "[e] Edit in $EDITOR";
                break;
            case 2:
                yield return "[j/k] Item";
                yield return "[Space] Toggle";
                yield return "[n] New item";
                break;
            case 3:
                yield return "[a] Comment";
                break;
            case 4:
                yield return "[j/k] Select";
                yield return "[Enter] Open";
                yield return "[d] Add dependency";
                break;
        }

        yield return "[s] " + SpecsPlansHintLabel();
        yield return "[Esc] Back";
        yield return "[q] Quit";
        yield return "[?] Help";
    }

    // Matches CardModal.vue's hasSpec/hasPlan gating (D-44): Idea has Specs only,
    // Task has Plans only, Goal/Issue have both.
    private string SpecsPlansHintLabel()
    {
        if (_card == null) return "Specs/Plans";
        var allowsSpec = CardTypeMapper.AllowsSpec(_card.Type);
        var allowsPlan = CardTypeMapper.AllowsPlan(_card.Type);
        if (allowsSpec && allowsPlan) return "Specs/Plans";
        return allowsSpec ? "Specs" : "Plans";
    }

    private Panel BuildSectionPanel(int index, bool isActive)
    {
        IRenderable inner;
        string header;

        switch (index)
        {
            case 0:
                header = " Metadata ";
                inner = BuildMetadataContent();
                break;
            case 1:
                header = " Description ";
                inner = BuildDescriptionContent();
                break;
            case 2:
                header = $" Checklist ({_checklist.Count(c => c.IsCompleted)}/{_checklist.Count}) ";
                inner = BuildChecklistContent();
                break;
            case 3:
                header = $" Comments ({_comments.Count}) ";
                inner = BuildCommentsContent();
                break;
            default:
                header = " Dependencies ";
                inner = BuildDependenciesContent();
                break;
        }

        return new Panel(inner)
        {
            Header = new PanelHeader(header),
            Border = isActive ? BoxBorder.Double : BoxBorder.Rounded,
            BorderStyle = isActive ? new Style(foreground: Color.Blue) : new Style(),
            Expand = true,
        };
    }

    private Panel BuildMetadataPanel() => BuildSectionPanel(0, _sectionIndex == 0);

    private Panel BuildDescriptionPanel() => BuildSectionPanel(1, _sectionIndex == 1);

    private Panel BuildChecklistPanel() => BuildSectionPanel(2, _sectionIndex == 2);

    private Panel BuildCommentsPanel() => BuildSectionPanel(3, _sectionIndex == 3);

    private Panel BuildDependenciesPanel() => BuildSectionPanel(4, _sectionIndex == 4);

    private IRenderable BuildMetadataContent()
    {
        if (_card == null)
            return new Markup("");

        var dueText = _card.DueAt.HasValue
            ? (
                _card.DueAt.Value.UtcDateTime < DateTime.UtcNow
                    ? $"[red]{_card.DueAt:yyyy-MM-dd} (overdue)[/]"
                    : $"[grey]{_card.DueAt:yyyy-MM-dd}[/]"
            )
            : "[grey]No due date[/]";

        var assignees =
            _card.Assignees?.Count > 0
                ? string.Join(", ", _card.Assignees.Select(a => Markup.Escape(a.Username)))
                : "[grey]Unassigned[/]";

        var watchingText = IsCurrentUserWatching()
            ? "[green]Yes[/]"
            : "[grey]No[/]";

        return new Rows(
            new Markup(
                $"Type: [{GetTypeColor(_card.Type)}]{CardTypeMapper.ToDisplayString(_card.Type)}[/]"
            ),
            new Markup($"Due: {dueText}"),
            new Markup($"Assignees: {assignees}"),
            new Markup($"Watching: {watchingText}"),
            new Markup($"Version: [grey]{_card.Version}[/]"),
            new Markup($"Created: [grey]{_card.CreatedAt:yyyy-MM-dd HH:mm}[/]")
        );
    }

    private Markup BuildDescriptionContent()
    {
        if (_card == null)
            return new Markup("");

        var desc = string.IsNullOrWhiteSpace(_card.Description)
            ? "[grey](No description)[/]"
            : Markup.Escape(
                _card.Description.Length > 200
                    ? _card.Description[..197] + "..."
                    : _card.Description
            );

        return new Markup(desc);
    }

    private IRenderable BuildChecklistContent()
    {
        if (_checklist.Count == 0)
            return new Markup("[grey](No checklist items)[/]");

        var showCursor = _sectionIndex == 2;
        var items = _checklist.Select(
            (item, i) =>
            {
                var isSelected = showCursor && i == _checklistIndex;
                var cursor = isSelected ? "[blue]>[/] " : "  ";
                var checkbox = item.IsCompleted ? "[green][[x]][/]" : "[grey][[ ]][/]";
                var text = item.IsCompleted
                    ? $"[strikethrough grey]{Markup.Escape(item.Text)}[/]"
                    : Markup.Escape(item.Text);
                return new Markup($"{cursor}{checkbox} {text}") as IRenderable;
            }
        );

        return new Rows([.. items]);
    }

    private IRenderable BuildCommentsContent()
    {
        if (_comments.Count == 0)
            return new Markup("[grey](No comments)[/]");

        var items = _comments.Select(c =>
        {
            var time = c.CreatedAt.ToString("MM-dd HH:mm");
            var author = Markup.Escape(c.AuthorUsername ?? "Unknown");
            return new Markup($"[bold]{author}[/] [grey]{time}[/]\n  {Markup.Escape(c.Content)}")
                as IRenderable;
        });

        return new Rows([.. items]);
    }

    private IRenderable BuildDependenciesContent()
    {
        if (_relationships.Count == 0)
            return new Markup("[grey](No dependencies)[/]");

        var showCursor = _sectionIndex == 4;
        var items = _relationships.Select(
            (r, i) =>
            {
                var isSelected = showCursor && i == _dependencyIndex;
                var cursor = isSelected ? "[blue]>[/] " : "  ";
                var desc = DescribeRelationship(r);
                return new Markup($"{cursor}{desc}") as IRenderable;
            }
        );

        return new Rows([.. items]);
    }

    // Relationship rows always carry Source/Target regardless of which side the
    // current card is on — direction and label must be resolved relative to
    // _cardId, otherwise the panel can show the card's own number as its "other side".
    private string DescribeRelationship(CardRelationshipDto r)
    {
        var isSource = r.SourceCardId == _cardId;

        return r.Type switch
        {
            RelationshipType.BlockedBy => isSource
                ? $"[red]blocks[/] #{r.TargetCardNumber} {Markup.Escape(r.TargetCardTitle)}"
                : $"[red]blocked by[/] #{r.SourceCardNumber} {Markup.Escape(r.SourceCardTitle)}",
            RelationshipType.Precedes => isSource
                ? $"[yellow]precedes[/] #{r.TargetCardNumber} {Markup.Escape(r.TargetCardTitle)}"
                : $"[yellow]preceded by[/] #{r.SourceCardNumber} {Markup.Escape(r.SourceCardTitle)}",
            RelationshipType.SpawnedFrom => isSource
                ? $"[cyan1]spawned from[/] #{r.TargetCardNumber} {Markup.Escape(r.TargetCardTitle)}"
                : $"[cyan1]spawned[/] #{r.SourceCardNumber} {Markup.Escape(r.SourceCardTitle)}",
            _ => isSource
                ? $"[grey]relates[/] #{r.TargetCardNumber} {Markup.Escape(r.TargetCardTitle)}"
                : $"[grey]relates[/] #{r.SourceCardNumber} {Markup.Escape(r.SourceCardTitle)}",
        };
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Tab when key.Modifiers == ConsoleModifiers.Shift:
                _sectionIndex = (_sectionIndex + 4) % 5;
                await RenderAsync();
                break;

            case ConsoleKey.Tab:
                _sectionIndex = (_sectionIndex + 1) % 5;
                await RenderAsync();
                break;

            case ConsoleKey.J
            or ConsoleKey.DownArrow:
            case ConsoleKey.N when key.Modifiers == ConsoleModifiers.Control:
                if (_sectionIndex == 2 && _checklist.Count > 0)
                {
                    _checklistIndex = Math.Min(_checklistIndex + 1, _checklist.Count - 1);
                    await RenderAsync();
                }
                else if (_sectionIndex == 4 && _relationships.Count > 0)
                {
                    _dependencyIndex = Math.Min(_dependencyIndex + 1, _relationships.Count - 1);
                    await RenderAsync();
                }
                break;

            case ConsoleKey.K
            or ConsoleKey.UpArrow:
            case ConsoleKey.P when key.Modifiers == ConsoleModifiers.Control:
                if (_sectionIndex == 2 && _checklistIndex > 0)
                {
                    _checklistIndex--;
                    await RenderAsync();
                }
                else if (_sectionIndex == 4 && _dependencyIndex > 0)
                {
                    _dependencyIndex--;
                    await RenderAsync();
                }
                break;

            case ConsoleKey.E:
                await EditCurrentSectionAsync();
                await RenderAsync();
                break;

            case ConsoleKey.Spacebar:
                if (_sectionIndex == 2)
                    await ToggleChecklistItemAsync();
                break;

            case ConsoleKey.N:
                if (_sectionIndex == 2)
                    await AddChecklistItemAsync();
                break;

            case ConsoleKey.A:
                if (_sectionIndex == 3)
                    await AddCommentAsync();
                break;

            case ConsoleKey.S:
                await OpenSpecsPlansAsync();
                break;

            case ConsoleKey.W:
                await ToggleWatchAsync();
                await RenderAsync();
                break;

            case ConsoleKey.D:
                _appState.PreviousScreen = this;
                var depPanel = new DependencyPanel(
                    _apiClientFactory,
                    _appState,
                    _errorCollector,
                    _projectId,
                    _cardId
                );
                _appState.CurrentScreen = depPanel;
                await depPanel.RenderAsync();
                break;

            case ConsoleKey.Enter:
                if (_sectionIndex == 4 && _relationships.Count > 0)
                {
                    var rel = _relationships[_dependencyIndex];
                    var targetCardId =
                        rel.SourceCardId == _cardId ? rel.TargetCardId : rel.SourceCardId;
                    _appState.SelectedCardId = targetCardId;
                    _appState.PreviousScreen = this;
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
                break;

            case ConsoleKey.Q:
                var confirm = AnsiConsole.Confirm("Quit HydraForge?");
                if (confirm)
                    Environment.Exit(0);
                break;

            case ConsoleKey.Escape:
                await OnExitAsync();
                var boardScreen = new BoardScreen(
                    _apiClientFactory,
                    _appState,
                    _errorCollector,
                    _connectionManager,
                    _signalRConnectionManager,
                    _notificationCenter
                );
                _appState.CurrentScreen = boardScreen;
                _appState.SelectedCardId = null;
                await boardScreen.OnEnterAsync();
                await boardScreen.RenderAsync();
                break;

            case ConsoleKey when key.KeyChar == '?':
                ShowHelp();
                await RenderAsync();
                break;
        }
    }

    private static void ShowHelp() =>
        HelpOverlay.Show(
            "Card Detail",
            [
                ("Tab / Shift+Tab", "Next / prev section"),
                ("j/k, ↑/↓, Ctrl+n/p", "Move in checklist / dependencies"),
                (
                    "e",
                    "Metadata: pick Title/Due Date/Assignees to edit; Description: edit in $EDITOR"
                ),
                ("Space", "Toggle checklist item"),
                ("n", "New checklist item (Checklist section only)"),
                ("a", "Add comment (Comments section only)"),
                ("w", "Toggle watching this card"),
                ("Enter", "Open dependency card (Dependencies section only)"),
                ("Esc", "Back to board"),
                ("q", "Quit"),
                ("?", "This help"),
            ]
        );

    // Which modes are offered mirrors CardModal.vue's hasSpec/hasPlan (D-44): Idea
    // is Specs-only, Task is Plans-only, Goal/Issue get both and pick via the prompt.
    private async Task OpenSpecsPlansAsync()
    {
        if (_card == null) return;

        var allowsSpec = CardTypeMapper.AllowsSpec(_card.Type);
        var allowsPlan = CardTypeMapper.AllowsPlan(_card.Type);

        string mode;
        if (allowsSpec && allowsPlan)
        {
            var viewIdx = await ListPrompt.Show(
                "View:",
                ["Specs", "Plans"],
                renderBackdrop: RenderAsync
            );
            if (!viewIdx.HasValue)
                return;
            mode = viewIdx == 0 ? "spec" : "plan";
        }
        else if (allowsSpec)
        {
            mode = "spec";
        }
        else
        {
            mode = "plan";
        }

        var specScreen = new SpecViewerScreen(
            _apiClientFactory, _appState, _errorCollector,
            _projectId, _cardId, _card.Type, mode);
        _appState.PreviousScreen = this;
        _appState.CurrentScreen = specScreen;
        await specScreen.OnEnterAsync();
        await specScreen.RenderAsync();
    }

    private async Task EditCurrentSectionAsync()
    {
        if (_card == null)
            return;

        var card = _card;
        switch (_sectionIndex)
        {
            case 0:
                var fieldIdx = await ListPrompt.Show(
                    "Edit field:",
                    ["Title", "Due Date", "Assignees"],
                    renderBackdrop: RenderAsync
                );
                switch (fieldIdx)
                {
                    case 0: // Title
                        var newTitle = AnsiConsole.Prompt(
                            new TextPrompt<string>(
                                "Title ([grey]Enter unchanged to cancel[/]):"
                            ).DefaultValue(card.Title)
                        );
                        if (newTitle != card.Title)
                            await UpdateCardAsync(title: newTitle);
                        break;

                    case 1: // Due Date
                        await EditDueDateAsync();
                        break;

                    case 2: // Assignees
                        await EditAssigneesAsync();
                        break;
                }
                break;

            case 1:
                var originalDesc = card.Description ?? "";
                var newDesc = await _editorLauncher.EditAsync(originalDesc);
                if (newDesc == null)
                {
                    AnsiConsole.MarkupLine(
                        "[red]Editor failed to launch — check $EDITOR/$VISUAL. No changes saved.[/]"
                    );
                }
                else if (newDesc != originalDesc)
                {
                    await UpdateCardAsync(description: newDesc);
                }
                break;
        }
    }

    private async Task EditDueDateAsync()
    {
        if (_card == null)
            return;

        var current = _card.DueAt.HasValue ? _card.DueAt.Value.ToString("yyyy-MM-dd") : "";
        var input = AnsiConsole.Prompt(
            new TextPrompt<string>("Due date (yyyy-MM-dd, [grey]blank to clear[/]):")
                .DefaultValue(current)
                .AllowEmpty()
        );

        if (string.IsNullOrWhiteSpace(input))
        {
            if (_card.DueAt.HasValue)
                await UpdateCardAsync(clearDueAt: true);
            return;
        }

        if (!DateTime.TryParse(input, out var parsed))
        {
            AnsiConsole.MarkupLine("[red]Invalid date. Use yyyy-MM-dd.[/]");
            return;
        }

        await UpdateCardAsync(
            dueAt: new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc))
        );
    }

    private async Task EditAssigneesAsync()
    {
        if (_card == null)
            return;

        try
        {
            var members = (await Client.MembersAllAsync(_projectId)).ToList();
            if (members.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No project members to assign.[/]");
                return;
            }

            var assignedIds = _card.Assignees?.Select(a => a.UserId).ToHashSet() ?? [];
            var labels = members
                .Select(m =>
                    assignedIds.Contains(m.UserId)
                        ? $"* {m.Username} (assigned)"
                        : $"  {m.Username}"
                )
                .ToList();

            var pickedIdx = await ListPrompt.Show(
                "Toggle assignee (Enter to select):",
                labels,
                renderBackdrop: RenderAsync
            );
            if (!pickedIdx.HasValue)
                return;

            var member = members[pickedIdx.Value];

            var updated = assignedIds.Contains(member.UserId)
                ? await Client.AssigneesDELETEAsync(_projectId, _cardId, member.UserId)
                : await Client.AssigneesPOSTAsync(
                    _projectId,
                    _cardId,
                    new AssignCardRequest { AssigneeUserId = member.UserId }
                );

            _card = updated;
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Assign failed: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Assign failed: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private bool IsCurrentUserWatching()
    {
        var userId = GetCurrentUserId();
        return userId.HasValue && (_card?.Watchers?.Any(w => w.UserId == userId.Value) ?? false);
    }

    private async Task ToggleWatchAsync()
    {
        if (_card == null)
            return;

        try
        {
            _card = IsCurrentUserWatching()
                ? await Client.WatchDELETEAsync(_projectId, _cardId)
                : await Client.WatchPOSTAsync(_projectId, _cardId);
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Toggle watch failed: {ex.Message}");
        }
    }

    // JWT `sub` claim decode — the TUI doesn't persist a decoded user id anywhere
    // (ConfigStore only stores the raw token), so this mirrors the Web UI's
    // useAuthStore.restoreToken() base64-decode approach on demand.
    private static Guid? GetCurrentUserId()
    {
        var token = new ConfigStore().Load().JwtToken;
        if (string.IsNullOrEmpty(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length < 2)
            return null;

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sub", out var sub)
                && Guid.TryParse(sub.GetString(), out var id)
                ? id
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task UpdateCardAsync(
        string? title = null,
        string? description = null,
        DateTimeOffset? dueAt = null,
        bool clearDueAt = false
    )
    {
        if (_card == null)
            return;

        try
        {
            var updated = await Client.CardsPUTAsync(
                _projectId,
                _cardId,
                new UpdateCardRequest
                {
                    Title = title ?? _card.Title,
                    Description = description ?? _card.Description,
                    Type = _card.Type,
                    ParentCardId = _card.ParentCardId,
                    DueAt = clearDueAt ? null : (dueAt ?? _card.DueAt),
                    Version = _card.Version,
                }
            );

            _card = updated;
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Update failed: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Update failed: {Markup.Escape(ex.Message)}[/]");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Connection error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private async Task ToggleChecklistItemAsync()
    {
        if (_checklist.Count == 0)
            return;
        var item = _checklist[_checklistIndex];

        try
        {
            await Client.ToggleAsync(_projectId, _cardId, item.Id);
            await LoadChecklistAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Toggle failed: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Toggle failed: {Markup.Escape(ex.Message)}[/]");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Connection error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private async Task AddChecklistItemAsync()
    {
        var text = AnsiConsole.Prompt(
            new TextPrompt<string>("Checklist item:").Validate(t =>
                string.IsNullOrWhiteSpace(t)
                    ? ValidationResult.Error("Text required")
                    : ValidationResult.Success()
            )
        );

        try
        {
            await Client.CardChecklistPOSTAsync(
                _projectId,
                _cardId,
                new CreateChecklistItemRequest { Text = text }
            );
            await LoadChecklistAsync();
            _checklistIndex = Math.Max(0, _checklist.Count - 1);
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Add item failed: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Add item failed: {Markup.Escape(ex.Message)}[/]");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Connection error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private async Task AddCommentAsync()
    {
        var content = AnsiConsole.Prompt(
            new TextPrompt<string>("Comment:").Validate(c =>
                string.IsNullOrWhiteSpace(c)
                    ? ValidationResult.Error("Comment required")
                    : ValidationResult.Success()
            )
        );

        try
        {
            await Client.CardCommentsPOSTAsync(
                _projectId,
                _cardId,
                new CreateCommentRequest { Content = content }
            );
            await LoadCommentsAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Comment failed: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Comment failed: {Markup.Escape(ex.Message)}[/]");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Connection error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private async Task LoadCardAsync()
    {
        try
        {
            _card = await Client.CardsGET2Async(_projectId, _cardId.ToString());

            await LoadChecklistAsync();
            await LoadCommentsAsync();
            await LoadRelationshipsAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Load failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task LoadChecklistAsync()
    {
        try
        {
            var list = await Client.CardChecklistGETAsync(_projectId, _cardId);
            _checklist = list?.Items?.ToList() ?? [];
            _checklistIndex = Math.Clamp(_checklistIndex, 0, Math.Max(0, _checklist.Count - 1));
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load checklist: {ex.Message}");
            _checklist = [];
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load checklist: {ex.Message}");
            _checklist = [];
        }
    }

    private async Task LoadCommentsAsync()
    {
        try
        {
            var list = await Client.CardCommentsGETAsync(_projectId, _cardId);
            _comments = list?.Comments?.ToList() ?? [];
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load comments: {ex.Message}");
            _comments = [];
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load comments: {ex.Message}");
            _comments = [];
        }
    }

    private async Task LoadRelationshipsAsync()
    {
        try
        {
            var list = await Client.CardRelationshipsGETAsync(_projectId, _cardId);
            _relationships = list?.Relationships?.ToList() ?? [];
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load relationships: {ex.Message}");
            _relationships = [];
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load relationships: {ex.Message}");
            _relationships = [];
        }
    }

    private static string GetTypeColor(CardType type) =>
        type switch
        {
            CardType.Task => "cyan1",
            CardType.Issue => "red",
            CardType.Goal => "yellow",
            CardType.Idea => "green",
            _ => "grey",
        };
}
