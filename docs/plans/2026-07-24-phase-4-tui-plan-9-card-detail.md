# Plan 9: Card Detail View

**Branch:** `task/tui-card-detail`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 9

**Goal:** Full-screen card detail with all fields, section navigation via Tab, $EDITOR for description, checklist/comments/dependencies display.

**Depends on:** Task 3 (ApiClientFactory), Task 7 (BoardScreen navigation).

---

## Step 1: Create `EditorLauncher` service

Create `src/HydraForge.Tui/Services/EditorLauncher.cs`:

```csharp
using System.Diagnostics;

namespace HydraForge.Tui.Services;

public class EditorLauncher
{
    public async Task<string?> EditAsync(string initialContent)
    {
        var tempFile = Path.GetTempFileName() + ".md";
        await File.WriteAllTextAsync(tempFile, initialContent);

        var editor = Environment.GetEnvironmentVariable("EDITOR")
                     ?? Environment.GetEnvironmentVariable("VISUAL")
                     ?? (OperatingSystem.IsWindows() ? "notepad.exe" : "vi");

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = editor,
                    Arguments = tempFile,
                    UseShellExecute = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            return await File.ReadAllTextAsync(tempFile);
        }
        catch (Exception ex)
        {
            return null; // Caller falls back to inline prompt
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }
}
```

## Step 2: Create `CardDetailScreen`

Create `src/HydraForge.Tui/Screens/CardDetailScreen.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class CardDetailScreen : IScreen
{
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private readonly EditorLauncher _editorLauncher = new();

    private CardDetail? _card;
    private List<ChecklistItem> _checklist = new();
    private List<CommentItem> _comments = new();
    private List<RelationshipItem> _relationships = new();
    private Guid _projectId;
    private Guid _cardId;
    private int _sectionIndex; // 0=metadata, 1=description, 2=checklist, 3=comments, 4=dependencies

    private static readonly JsonSerializerOptions JsonOptions = new()
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
        var typeColor = _card.Type switch
        {
            "Task" => "cyan1",
            "Issue" => "red",
            "Goal" => "yellow",
            "Idea" => "green",
            _ => "grey"
        };

        var header = new Rule(
            $"[{typeColor}]#{_card.CardNumber}[/] [blue bold]{Markup.Escape(_card.Title)}[/]"
        ).LeftAligned();
        AnsiConsole.Write(header);

        // Metadata section
        var metadataPanel = BuildMetadataPanel();
        // Description section
        var descriptionPanel = BuildDescriptionPanel();
        // Checklist section
        var checklistPanel = BuildChecklistPanel();
        // Comments section
        var commentsPanel = BuildCommentsPanel();
        // Dependencies section
        var dependenciesPanel = BuildDependenciesPanel();

        var sections = new List<IRenderable>
        {
            metadataPanel,
            descriptionPanel,
            checklistPanel,
            commentsPanel,
            dependenciesPanel
        };

        // Highlight active section
        if (_sectionIndex < sections.Count)
        {
            var active = sections[_sectionIndex];
            sections[_sectionIndex] = new Panel((active as Panel)?.Content ?? new Markup(""))
            {
                Border = BoxBorder.Double,
                BorderColor = Color.Blue,
                Header = (active as Panel)?.Header
            };
        }

        AnsiConsole.Write(new Rows(sections));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey][[Tab]] Sections  [[e]] Edit  [[Space]] Toggle  [[a]] Comment  [[d]] Dependency  [[s]] Spec/Plan  [[Esc]] Back[/]");
    }

    private Panel BuildMetadataPanel()
    {
        var dueText = _card!.DueAt.HasValue
            ? (_card.DueAt.Value < DateTime.UtcNow
                ? $"[red]{_card.DueAt:yyyy-MM-dd} (overdue)[/]"
                : $"[grey]{_card.DueAt:yyyy-MM-dd}[/]")
            : "[grey]No due date[/]";

        var assignees = _card.Assignees.Count > 0
            ? string.Join(", ", _card.Assignees.Select(a => a.Username))
            : "[grey]Unassigned[/]";

        var content = new Rows(
            new Markup($"Type: [{GetTypeColor(_card.Type)}]{_card.Type}[/]"),
            new Markup($"Status: [grey]{_card.ColumnName}[/]"),
            new Markup($"Due: {dueText}"),
            new Markup($"Assignees: {assignees}"),
            new Markup($"Version: [grey]{_card.Version}[/]"),
            new Markup($"Created: [grey]{_card.CreatedAt:yyyy-MM-dd HH:mm}[/]")
        );

        return new Panel(content)
        {
            Header = new PanelHeader(" Metadata "),
            Border = BoxBorder.Rounded,
        };
    }

    private Panel BuildDescriptionPanel()
    {
        var desc = string.IsNullOrWhiteSpace(_card!.Description)
            ? "[grey](No description)[/]"
            : Markup.Escape(_card.Description.Length > 200
                ? _card.Description[..197] + "..."
                : _card.Description);

        return new Panel(new Markup(desc))
        {
            Header = new PanelHeader(" Description "),
            Border = BoxBorder.Rounded,
        };
    }

    private Panel BuildChecklistPanel()
    {
        if (_checklist.Count == 0)
        {
            return new Panel(new Markup("[grey](No checklist items)[/]"))
            {
                Header = new PanelHeader(" Checklist "),
                Border = BoxBorder.Rounded,
            };
        }

        var items = _checklist.Select(item =>
        {
            var checkbox = item.IsCompleted ? "[green][[x]][/]" : "[grey][[ ]][/]";
            var text = item.IsCompleted
                ? $"[strikethrough grey]{Markup.Escape(item.Text)}[/]"
                : Markup.Escape(item.Text);
            return new Markup($"{checkbox} {text}");
        });

        return new Panel(new Rows(items.Cast<IRenderable>().ToList()))
        {
            Header = new PanelHeader($" Checklist ({_checklist.Count(c => c.IsCompleted)}/{_checklist.Count}) "),
            Border = BoxBorder.Rounded,
        };
    }

    private Panel BuildCommentsPanel()
    {
        if (_comments.Count == 0)
        {
            return new Panel(new Markup("[grey](No comments)[/]"))
            {
                Header = new PanelHeader(" Comments "),
                Border = BoxBorder.Rounded,
            };
        }

        var items = _comments.Select(c =>
        {
            var time = c.CreatedAt.ToString("MM-dd HH:mm");
            return new Markup($"[bold]{Markup.Escape(c.AuthorUsername)}[/] [grey]{time}[/]\n  {Markup.Escape(c.Content)}");
        });

        return new Panel(new Rows(items.Cast<IRenderable>().ToList()))
        {
            Header = new PanelHeader($" Comments ({_comments.Count}) "),
            Border = BoxBorder.Rounded,
        };
    }

    private Panel BuildDependenciesPanel()
    {
        if (_relationships.Count == 0)
        {
            return new Panel(new Markup("[grey](No dependencies)[/]"))
            {
                Header = new PanelHeader(" Dependencies "),
                Border = BoxBorder.Rounded,
            };
        }

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
            return new Markup($"{typeLabel} #{r.TargetCardNumber} {Markup.Escape(r.TargetCardTitle)}");
        });

        return new Panel(new Rows(items.Cast<IRenderable>().ToList()))
        {
            Header = new PanelHeader(" Dependencies "),
            Border = BoxBorder.Rounded,
        };
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Tab:
                _sectionIndex = (_sectionIndex + 1) % 5;
                await RenderAsync();
                break;

            case ConsoleKey.Tab when key.Modifiers == ConsoleModifiers.Shift:
                _sectionIndex = (_sectionIndex + 4) % 5;
                await RenderAsync();
                break;

            case ConsoleKey.E:
                await EditCurrentSectionAsync();
                break;

            case ConsoleKey.Spacebar:
                if (_sectionIndex == 2) // Checklist
                    await ToggleChecklistItemAsync();
                break;

            case ConsoleKey.A:
                if (_sectionIndex == 3) // Comments
                    await AddCommentAsync();
                break;

            case ConsoleKey.D:
                // Dependency panel — Task 11
                break;

            case ConsoleKey.S:
                // Spec/Plan viewer — Task 13
                break;

            case ConsoleKey.Escape:
                _appState.CurrentScreen = null; // Back to board
                break;
        }
    }

    private async Task EditCurrentSectionAsync()
    {
        switch (_sectionIndex)
        {
            case 0: // Metadata — edit title inline
                var newTitle = AnsiConsole.Prompt(
                    new TextPrompt<string>("Title:")
                        .DefaultValue(_card!.Title));
                await UpdateCardAsync(title: newTitle);
                break;

            case 1: // Description — $EDITOR
                var newDesc = await _editorLauncher.EditAsync(_card!.Description);
                if (newDesc != null)
                    await UpdateCardAsync(description: newDesc);
                break;
        }
    }

    private async Task UpdateCardAsync(string? title = null, string? description = null)
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var payload = new
            {
                title = title ?? _card!.Title,
                description = description ?? _card!.Description,
                type = _card!.Type,
                parentCardId = (Guid?)null,
                dueAt = _card!.DueAt,
                version = _card!.Version
            };

            var response = await client.PutAsJsonAsync(
                $"api/projects/{_projectId}/cards/{_cardId}", payload, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                _card = await response.Content.ReadFromJsonAsync<CardDetail>(JsonOptions);
                await RenderAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _errorCollector.Add("N/A", $"Update failed: {error}");
            }
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task ToggleChecklistItemAsync()
    {
        if (_checklist.Count == 0) return;
        // Toggle first incomplete item, or first item
        var item = _checklist.FirstOrDefault(i => !i.IsCompleted) ?? _checklist[0];

        try
        {
            var client = _apiClientFactory.GetClient();
            var response = await client.PatchAsync(
                $"api/projects/{_projectId}/cards/{_cardId}/checklist/{item.Id}/toggle", null);

            if (response.IsSuccessStatusCode)
            {
                await LoadChecklistAsync();
                await RenderAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Toggle error: {ex.Message}");
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
            var payload = new { content };

            var response = await client.PostAsJsonAsync(
                $"api/projects/{_projectId}/cards/{_cardId}/comments", payload, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                await LoadCommentsAsync();
                await RenderAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Comment error: {ex.Message}");
        }
    }

    private async Task LoadCardAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var response = await client.GetAsync(
                $"api/projects/{_projectId}/cards/{_cardId}");

            if (response.IsSuccessStatusCode)
            {
                _card = await response.Content.ReadFromJsonAsync<CardDetail>(JsonOptions);
            }

            await LoadChecklistAsync();
            await LoadCommentsAsync();
            await LoadRelationshipsAsync();
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Load error: {ex.Message}");
        }
    }

    private async Task LoadChecklistAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var response = await client.GetAsync(
                $"api/projects/{_projectId}/cards/{_cardId}/checklist");

            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<ChecklistList>(JsonOptions);
                _checklist = list?.Items ?? new();
            }
        }
        catch { }
    }

    private async Task LoadCommentsAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var response = await client.GetAsync(
                $"api/projects/{_projectId}/cards/{_cardId}/comments");

            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<CommentList>(JsonOptions);
                _comments = list?.Comments ?? new();
            }
        }
        catch { }
    }

    private async Task LoadRelationshipsAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var response = await client.GetAsync(
                $"api/projects/{_projectId}/cards/{_cardId}/relationships");

            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<RelationshipList>(JsonOptions);
                _relationships = list?.Relationships ?? new();
            }
        }
        catch { }
    }

    private static string GetTypeColor(string type) => type switch
    {
        "Task" => "cyan1",
        "Issue" => "red",
        "Goal" => "yellow",
        "Idea" => "green",
        _ => "grey"
    };

    // DTOs
    private record CardDetail(
        Guid Id, int CardNumber, string Title, string Description, string Type,
        string ColumnName, int Version, DateTime CreatedAt, DateTime? DueAt,
        List<AssigneeInfo> Assignees
    );
    private record AssigneeInfo(Guid UserId, string Username);
    private record ChecklistList(List<ChecklistItem> Items);
    private record ChecklistItem(Guid Id, string Text, bool IsCompleted, int Position);
    private record CommentList(List<CommentItem> Comments);
    private record CommentItem(Guid Id, string AuthorUsername, string Content, DateTime CreatedAt);
    private record RelationshipList(List<RelationshipItem> Relationships);
    private record RelationshipItem(Guid Id, string Type, int TargetCardNumber, string TargetCardTitle);
}
```

## Step 3: Wire `CardDetailScreen` into `BoardScreen`

Update `BoardScreen.OpenCardDetailAsync`:
```csharp
private async Task OpenCardDetailAsync()
{
    if (_columns.Count == 0) return;
    var col = _columns[_selectedColumn];
    if (_selectedCard >= col.Cards.Count) return;

    var card = col.Cards[_selectedCard];
    _appState.SelectedCardId = card.Id;

    var detailScreen = new CardDetailScreen(_apiClientFactory, _appState, _errorCollector);
    _appState.CurrentScreen = detailScreen;
    await detailScreen.OnEnterAsync();
    await detailScreen.RenderAsync();
}
```

## Step 4: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 5: Commit

```bash
git add src/HydraForge.Tui/Services/EditorLauncher.cs src/HydraForge.Tui/Screens/CardDetailScreen.cs src/HydraForge.Tui/Screens/BoardScreen.cs
git commit -m "feat(tui): add card detail view with sections, editor, checklist, comments"
```