using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace HydraForge.Tui.Screens;

public class CardDetailScreen : IScreen
{
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private readonly ConnectionManager _connectionManager;
    private readonly EditorLauncher _editorLauncher = new();

    private CardResponse? _card;
    private List<ChecklistItemResponse> _checklist = new();
    private List<CommentResponse> _comments = new();
    private List<CardRelationshipDto> _relationships = new();
    private Guid _projectId;
    private Guid _cardId;
    private int _sectionIndex;
    private int _checklistIndex;

    public CardDetailScreen(
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
        _cardId = _appState.SelectedCardId ?? Guid.Empty;
        await LoadCardAsync();
    }

    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        if (_card == null) return;

        AnsiConsole.Clear();

        // Header
        var typeColor = GetTypeColor(_card.Type);
        AnsiConsole.Write(new Rule(
            $"[{typeColor}]#{_card.CardNumber}[/] [blue bold]{Markup.Escape(_card.Title)}[/]"
        ));

        // Sections — build content first, then wrap in panels
        var content = new List<IRenderable>
        {
            BuildMetadataPanel(),
            BuildDescriptionPanel(),
            BuildChecklistPanel(),
            BuildCommentsPanel(),
            BuildDependenciesPanel()
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
                yield return "[e] Edit title";
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
        }

        yield return "[Esc] Back";
        yield return "[q] Quit";
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
            BorderStyle = isActive ? new Style(foreground: Color.Blue) : null,
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
        if (_card == null) return new Markup("");

        var dueText = _card.DueAt.HasValue
            ? (_card.DueAt.Value.UtcDateTime < DateTime.UtcNow
                ? $"[red]{_card.DueAt:yyyy-MM-dd} (overdue)[/]"
                : $"[grey]{_card.DueAt:yyyy-MM-dd}[/]")
            : "[grey]No due date[/]";

        var assignees = _card.Assignees?.Count > 0
            ? string.Join(", ", _card.Assignees.Select(a => Markup.Escape(a.Username)))
            : "[grey]Unassigned[/]";

        return new Rows(
            new Markup($"Type: [{GetTypeColor(_card.Type)}]{CardTypeMapper.ToDisplayString(_card.Type)}[/]"),
            new Markup($"Due: {dueText}"),
            new Markup($"Assignees: {assignees}"),
            new Markup($"Version: [grey]{_card.Version}[/]"),
            new Markup($"Created: [grey]{_card.CreatedAt:yyyy-MM-dd HH:mm}[/]")
        );
    }

    private IRenderable BuildDescriptionContent()
    {
        if (_card == null) return new Markup("");

        var desc = string.IsNullOrWhiteSpace(_card.Description)
            ? "[grey](No description)[/]"
            : Markup.Escape(_card.Description.Length > 200
                ? _card.Description[..197] + "..."
                : _card.Description);

        return new Markup(desc);
    }

    private IRenderable BuildChecklistContent()
    {
        if (_checklist.Count == 0)
            return new Markup("[grey](No checklist items)[/]");

        var showCursor = _sectionIndex == 2;
        var items = _checklist.Select((item, i) =>
        {
            var isSelected = showCursor && i == _checklistIndex;
            var cursor = isSelected ? "[blue]>[/] " : "  ";
            var checkbox = item.IsCompleted ? "[green][[x]][/]" : "[grey][[ ]][/]";
            var text = item.IsCompleted
                ? $"[strikethrough grey]{Markup.Escape(item.Text)}[/]"
                : Markup.Escape(item.Text);
            return new Markup($"{cursor}{checkbox} {text}") as IRenderable;
        });

        return new Rows(items.ToArray());
    }

    private IRenderable BuildCommentsContent()
    {
        if (_comments.Count == 0)
            return new Markup("[grey](No comments)[/]");

        var items = _comments.Select(c =>
        {
            var time = c.CreatedAt.ToString("MM-dd HH:mm");
            var author = Markup.Escape(c.AuthorUsername ?? "Unknown");
            return new Markup($"[bold]{author}[/] [grey]{time}[/]\n  {Markup.Escape(c.Content)}") as IRenderable;
        });

        return new Rows(items.ToArray());
    }

    private IRenderable BuildDependenciesContent()
    {
        if (_relationships.Count == 0)
            return new Markup("[grey](No dependencies)[/]");

        var items = _relationships.Select(r => new Markup(DescribeRelationship(r)) as IRenderable);

        return new Rows(items.ToArray());
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

            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (_sectionIndex == 2 && _checklist.Count > 0)
                {
                    _checklistIndex = Math.Min(_checklistIndex + 1, _checklist.Count - 1);
                    await RenderAsync();
                }
                break;

            case ConsoleKey.K or ConsoleKey.UpArrow:
                if (_sectionIndex == 2 && _checklistIndex > 0)
                {
                    _checklistIndex--;
                    await RenderAsync();
                }
                break;

            case ConsoleKey.E:
                await EditCurrentSectionAsync();
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
                break;

            case ConsoleKey.Q:
                var confirm = AnsiConsole.Confirm("Quit HydraForge?");
                if (confirm)
                    Environment.Exit(0);
                break;

            case ConsoleKey.Escape:
                await OnExitAsync();
                var boardScreen = new BoardScreen(
                    _apiClientFactory, _appState, _errorCollector, _connectionManager);
                _appState.CurrentScreen = boardScreen;
                _appState.SelectedCardId = null;
                await boardScreen.OnEnterAsync();
                await boardScreen.RenderAsync();
                break;
        }
    }

    private async Task EditCurrentSectionAsync()
    {
        if (_card == null) return;

        var card = _card;
        switch (_sectionIndex)
        {
            case 0:
                var newTitle = AnsiConsole.Prompt(
                    new TextPrompt<string>("Title ([grey]Enter unchanged to cancel[/]):")
                        .DefaultValue(card.Title));
                if (newTitle != card.Title)
                    await UpdateCardAsync(title: newTitle);
                break;

            case 1:
                var originalDesc = card.Description ?? "";
                var newDesc = await _editorLauncher.EditAsync(originalDesc);
                if (newDesc == null)
                {
                    AnsiConsole.MarkupLine("[red]Editor failed to launch — check $EDITOR/$VISUAL. No changes saved.[/]");
                }
                else if (newDesc != originalDesc)
                {
                    await UpdateCardAsync(description: newDesc);
                }
                break;
        }
    }

    private async Task UpdateCardAsync(string? title = null, string? description = null)
    {
        if (_card == null) return;

        try
        {
            var client = _apiClientFactory.GetClient();
            var updated = await client.CardsPUTAsync(_projectId, _cardId, new UpdateCardRequest
            {
                Title = title ?? _card.Title,
                Description = description ?? _card.Description,
                Type = _card.Type,
                ParentCardId = _card.ParentCardId,
                DueAt = _card.DueAt,
                Version = _card.Version
            });

            _card = updated;
            await RenderAsync();
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
        if (_checklist.Count == 0) return;
        var item = _checklist[_checklistIndex];

        try
        {
            var client = _apiClientFactory.GetClient();
            await client.ToggleAsync(_projectId, _cardId, item.Id);
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
            new TextPrompt<string>("Checklist item:")
                .Validate(t => string.IsNullOrWhiteSpace(t)
                    ? ValidationResult.Error("Text required")
                    : ValidationResult.Success()));

        try
        {
            var client = _apiClientFactory.GetClient();
            await client.CardChecklistPOSTAsync(_projectId, _cardId, new CreateChecklistItemRequest
            {
                Text = text
            });
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
            new TextPrompt<string>("Comment:")
                .Validate(c => string.IsNullOrWhiteSpace(c)
                    ? ValidationResult.Error("Comment required")
                    : ValidationResult.Success()));

        try
        {
            var client = _apiClientFactory.GetClient();
            await client.CardCommentsPOSTAsync(_projectId, _cardId, new CreateCommentRequest
            {
                Content = content
            });
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
            var client = _apiClientFactory.GetClient();
            _card = await client.CardsGET2Async(_projectId, _cardId.ToString());

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
            var client = _apiClientFactory.GetClient();
            var list = await client.CardChecklistGETAsync(_projectId, _cardId);
            _checklist = list?.Items?.ToList() ?? new();
            _checklistIndex = Math.Clamp(_checklistIndex, 0, Math.Max(0, _checklist.Count - 1));
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load checklist: {ex.Message}");
            _checklist = new();
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load checklist: {ex.Message}");
            _checklist = new();
        }
    }

    private async Task LoadCommentsAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var list = await client.CardCommentsGETAsync(_projectId, _cardId);
            _comments = list?.Comments?.ToList() ?? new();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load comments: {ex.Message}");
            _comments = new();
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load comments: {ex.Message}");
            _comments = new();
        }
    }

    private async Task LoadRelationshipsAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var list = await client.CardRelationshipsGETAsync(_projectId, _cardId);
            _relationships = list?.Relationships?.ToList() ?? new();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load relationships: {ex.Message}");
            _relationships = new();
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Failed to load relationships: {ex.Message}");
            _relationships = new();
        }
    }

    private static string GetTypeColor(CardType type) => type switch
    {
        CardType.Task => "cyan1",
        CardType.Issue => "red",
        CardType.Goal => "yellow",
        CardType.Idea => "green",
        _ => "grey"
    };
}
