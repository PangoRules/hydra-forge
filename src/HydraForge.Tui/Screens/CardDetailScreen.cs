using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly EditorLauncher _editorLauncher = new();

    private CardResponse? _card;
    private List<ChecklistItemData> _checklist = new();
    private List<CommentData> _comments = new();
    private List<RelationshipData> _relationships = new();
    private Guid _projectId;
    private Guid _cardId;
    private int _sectionIndex;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CardDetailScreen(
        ApiClientFactory apiClientFactory,
        AppState appState,
        ErrorCollector errorCollector)
    {
        _apiClientFactory = apiClientFactory;
        _appState = appState;
        _errorCollector = errorCollector;
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
        KeyHintBar.Render(new[]
        {
            "[Tab] Sections", "[e] Edit", "[Space] Toggle", "[a] Comment",
            "[Esc] Back",
        });
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

        var items = _checklist.Select(item =>
        {
            var checkbox = item.IsCompleted ? "[green][[x]][/]" : "[grey][[ ]][/]";
            var text = item.IsCompleted
                ? $"[strikethrough grey]{Markup.Escape(item.Text)}[/]"
                : Markup.Escape(item.Text);
            return new Markup($"{checkbox} {text}") as IRenderable;
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

        var items = _relationships.Select(r =>
        {
            var typeLabel = r.Type switch
            {
                "BlockedBy" => "[red]blocks[/]",
                "Precedes" => "[yellow]precedes[/]",
                "Relates" => "[grey]relates[/]",
                "SpawnedFrom" => "[cyan1]spawned[/]",
                _ => r.Type
            };
            return new Markup($"{typeLabel} #{r.TargetCardNumber} {Markup.Escape(r.TargetCardTitle)}") as IRenderable;
        });

        return new Rows(items.ToArray());
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

            case ConsoleKey.E:
                await EditCurrentSectionAsync();
                break;

            case ConsoleKey.Spacebar:
                if (_sectionIndex == 2)
                    await ToggleChecklistItemAsync();
                break;

            case ConsoleKey.A:
                if (_sectionIndex == 3)
                    await AddCommentAsync();
                break;

            case ConsoleKey.S:
                break;

            case ConsoleKey.Escape:
                _appState.CurrentScreen = null;
                break;
        }
    }

    private async Task EditCurrentSectionAsync()
    {
        if (_card == null) return;
        
        var card = _card!;
        switch (_sectionIndex)
        {
            case 0:
                var newTitle = AnsiConsole.Prompt(
                    new TextPrompt<string>("Title:")
                        .DefaultValue(card.Title));
                if (newTitle != card.Title)
                    await UpdateCardAsync(title: newTitle);
                break;

            case 1:
                var originalDesc = card.Description ?? "";
                var newDesc = await _editorLauncher.EditAsync(originalDesc);
                if (newDesc != null && newDesc != originalDesc)
                    await UpdateCardAsync(description: newDesc);
                break;
        }
    }

    private async Task UpdateCardAsync(string? title = null, string? description = null)
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var updated = await client.CardsPUTAsync(_projectId, _cardId, new UpdateCardRequest
            {
                Title = title ?? _card!.Title,
                Description = description ?? _card!.Description,
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
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task ToggleChecklistItemAsync()
    {
        if (_checklist.Count == 0) return;
        var item = _checklist.FirstOrDefault(i => !i.IsCompleted) ?? _checklist[0];

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
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
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
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
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
            var http = _apiClientFactory.CreateAuthenticatedHttpClient();
            var response = await http.GetAsync($"api/projects/{_projectId}/cards/{_cardId}/CardChecklist");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<ChecklistWrapper>(json, JsonOpts);
            _checklist = wrapper?.Items ?? new();
        }
        catch (Exception ex)
        {
            _errorCollector.Add("N/A", $"Failed to load [checklist]: {ex.Message}");
            _checklist = new();
        }
    }

    private async Task LoadCommentsAsync()
    {
        try
        {
            var http = _apiClientFactory.CreateAuthenticatedHttpClient();
            var response = await http.GetAsync($"api/projects/{_projectId}/cards/{_cardId}/CardComments");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<CommentsWrapper>(json, JsonOpts);
            _comments = wrapper?.Comments ?? new();
        }
        catch (Exception ex)
        {
            _errorCollector.Add("N/A", $"Failed to load [comments]: {ex.Message}");
            _comments = new();
        }
    }

    private async Task LoadRelationshipsAsync()
    {
        try
        {
            var http = _apiClientFactory.CreateAuthenticatedHttpClient();
            var response = await http.GetAsync($"api/projects/{_projectId}/cards/{_cardId}/CardRelationships");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<RelationshipsWrapper>(json, JsonOpts);
            _relationships = wrapper?.Relationships ?? new();
        }
        catch (Exception ex)
        {
            _errorCollector.Add("N/A", $"Failed to load [relationships]: {ex.Message}");
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

    // Raw JSON DTOs for NSwag-void endpoints

    private class ChecklistWrapper
    {
        [JsonPropertyName("items")]
        public List<ChecklistItemData> Items { get; set; } = new();
    }

    private class ChecklistItemData
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("isCompleted")]
        public bool IsCompleted { get; set; }
    }

    private class CommentsWrapper
    {
        [JsonPropertyName("comments")]
        public List<CommentData> Comments { get; set; } = new();
    }

    private class CommentData
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonPropertyName("authorUsername")]
        public string AuthorUsername { get; set; } = "";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    private class RelationshipsWrapper
    {
        [JsonPropertyName("relationships")]
        public List<RelationshipData> Relationships { get; set; } = new();
    }

    private class RelationshipData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("targetCardNumber")]
        public int TargetCardNumber { get; set; }

        [JsonPropertyName("targetCardTitle")]
        public string TargetCardTitle { get; set; } = "";
    }
}