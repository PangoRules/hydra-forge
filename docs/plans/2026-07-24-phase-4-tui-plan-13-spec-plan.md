# Plan 13: Spec + Plan Viewer/Editor

**Branch:** `task/tui-spec-plan`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 13

**Goal:** View spec/plan documents in terminal, open in $EDITOR for editing, save back via API.

**Depends on:** Task 3 (ApiClientFactory), Task 9 (CardDetailScreen, EditorLauncher).

---

## Step 1: Create `SpecViewerScreen`

Create `src/HydraForge.Tui/Screens/SpecViewerScreen.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class SpecViewerScreen : IScreen
{
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private readonly EditorLauncher _editorLauncher = new();

    private readonly Guid _projectId;
    private readonly Guid _cardId;
    private readonly string _mode; // "spec" or "plan"

    private List<DocumentItem> _documents = new();
    private int _selectedIndex;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SpecViewerScreen(
        ApiClientFactory apiClientFactory,
        AppState appState,
        ErrorCollector errorCollector,
        Guid projectId,
        Guid cardId,
        string mode = "spec")
    {
        _apiClientFactory = apiClientFactory;
        _appState = appState;
        _errorCollector = errorCollector;
        _projectId = projectId;
        _cardId = cardId;
        _mode = mode;
    }

    public async Task OnEnterAsync()
    {
        await LoadDocumentsAsync();
    }

    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        AnsiConsole.Clear();

        var title = _mode == "spec" ? "Specifications" : "Plans";
        AnsiConsole.Write(new Rule($"[blue]{title}[/]").LeftAligned());

        if (_documents.Count == 0)
        {
            AnsiConsole.MarkupLine($"[grey]No {_mode}s for this card.[/]");
            AnsiConsole.MarkupLine("[grey][[c]] Create  [[Esc]] Back[/]");
            return;
        }

        for (int i = 0; i < _documents.Count; i++)
        {
            var doc = _documents[i];
            var isSelected = i == _selectedIndex;
            var prefix = isSelected ? "[blue]>[/]" : " ";

            var statusBadge = "";
            if (_mode == "plan" && doc.Status != null)
            {
                statusBadge = doc.Status switch
                {
                    "Pending" => " [grey](pending)[/]",
                    "Active" => " [green](active)[/]",
                    "Done" => " [green](done)[/]",
                    _ => ""
                };
            }

            var typeBadge = doc.DocType != null
                ? $" [{GetTypeColor(doc.DocType)}]{doc.DocType}[/]"
                : "";

            AnsiConsole.MarkupLine(
                $"{prefix} [bold]{Markup.Escape(doc.Title)}[/]{typeBadge}{statusBadge}");
            AnsiConsole.MarkupLine($"   [grey]v{doc.Version} — {doc.UpdatedAt:yyyy-MM-dd HH:mm}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey][[Enter]] View  [[e]] Edit  [[c]] Create  [[Esc]] Back[/]");
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (_selectedIndex < _documents.Count - 1)
                    _selectedIndex++;
                await RenderAsync();
                break;

            case ConsoleKey.K or ConsoleKey.UpArrow:
                if (_selectedIndex > 0)
                    _selectedIndex--;
                await RenderAsync();
                break;

            case ConsoleKey.Enter:
                await ViewDocumentAsync();
                break;

            case ConsoleKey.E:
                await EditDocumentAsync();
                break;

            case ConsoleKey.C:
                await CreateDocumentAsync();
                break;

            case ConsoleKey.Escape:
                _appState.CurrentScreen = null;
                break;
        }
    }

    private async Task ViewDocumentAsync()
    {
        if (_selectedIndex >= _documents.Count) return;
        var doc = _documents[_selectedIndex];

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[blue]{Markup.Escape(doc.Title)}[/]").LeftAligned());

        // Render content (truncated for terminal)
        var content = doc.Content.Length > 1000
            ? doc.Content[..997] + "..."
            : doc.Content;

        var panel = new Panel(new Markup(Markup.Escape(content)))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader($" {_mode} v{doc.Version} "),
        };
        AnsiConsole.Write(panel);

        AnsiConsole.MarkupLine("[grey]Press any key to return[/]");
        Console.ReadKey(true);
        await RenderAsync();
    }

    private async Task EditDocumentAsync()
    {
        if (_selectedIndex >= _documents.Count) return;
        var doc = _documents[_selectedIndex];

        var newContent = await _editorLauncher.EditAsync(doc.Content);
        if (newContent == null)
        {
            AnsiConsole.MarkupLine("[yellow]Editor failed. Using inline prompt.[/]");
            newContent = AnsiConsole.Prompt(
                new TextPrompt<string>("Content:")
                    .DefaultValue(doc.Content));
        }

        try
        {
            var client = _apiClientFactory.GetClient();
            var payload = new
            {
                title = doc.Title,
                description = (string?)null,
                content = newContent
            };

            var url = _mode == "spec"
                ? $"api/projects/{_projectId}/specs/{doc.Id}"
                : $"api/projects/{_projectId}/plans/{doc.Id}";

            var response = await client.PutAsJsonAsync(url, payload, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine("[green]Document updated.[/]");
                await LoadDocumentsAsync();
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

    private async Task CreateDocumentAsync()
    {
        var title = AnsiConsole.Prompt(
            new TextPrompt<string>("Title:")
                .Validate(t => string.IsNullOrWhiteSpace(t)
                    ? ValidationResult.Error("Title required")
                    : ValidationResult.Success()));

        var docType = "Specification";
        if (_mode == "spec")
        {
            docType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Doc type:")
                    .AddChoices("Specification", "Concept", "Report"));
        }

        var content = AnsiConsole.Prompt(
            new TextPrompt<string>("Content (markdown):")
                .DefaultValue($"# {title}\n\n"));

        try
        {
            var client = _apiClientFactory.GetClient();

            if (_mode == "spec")
            {
                var payload = new
                {
                    docType,
                    title,
                    description = (string?)null,
                    content
                };
                var response = await client.PostAsJsonAsync(
                    $"api/projects/{_projectId}/specs/cards/{_cardId}", payload, JsonOptions);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _errorCollector.Add("N/A", $"Create failed: {error}");
                    return;
                }
            }
            else
            {
                var payload = new
                {
                    title,
                    description = (string?)null,
                    content,
                    specId = (Guid?)null,
                    position = _documents.Count
                };
                var response = await client.PostAsJsonAsync(
                    $"api/projects/{_projectId}/plans/cards/{_cardId}", payload, JsonOptions);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _errorCollector.Add("N/A", $"Create failed: {error}");
                    return;
                }
            }

            AnsiConsole.MarkupLine($"[green]{_mode} created![/]");
            await LoadDocumentsAsync();
            await RenderAsync();
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            var url = _mode == "spec"
                ? $"api/projects/{_projectId}/specs/cards/{_cardId}"
                : $"api/projects/{_projectId}/plans/cards/{_cardId}";

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                if (_mode == "spec")
                {
                    var list = await response.Content.ReadFromJsonAsync<SpecList>(JsonOptions);
                    _documents = list?.Specs.Select(s => new DocumentItem(
                        s.Id, s.Title, s.Content, s.Version, s.UpdatedAt, s.DocType, null
                    )).ToList() ?? new();
                }
                else
                {
                    var list = await response.Content.ReadFromJsonAsync<PlanList>(JsonOptions);
                    _documents = list?.Plans.Select(p => new DocumentItem(
                        p.Id, p.Title, p.Content, p.Version, p.UpdatedAt, null, p.Status
                    )).ToList() ?? new();
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Load error: {ex.Message}");
        }
    }

    private static string GetTypeColor(string type) => type switch
    {
        "Specification" => "blue",
        "Concept" => "yellow",
        "Report" => "green",
        _ => "grey"
    };

    private record DocumentItem(
        Guid Id, string Title, string Content, int Version,
        DateTime UpdatedAt, string? DocType, string? Status
    );

    private record SpecList(List<SpecInfo> Specs);
    private record SpecInfo(Guid Id, string Title, string Content, int Version,
        DateTime UpdatedAt, string DocType);
    private record PlanList(List<PlanInfo> Plans);
    private record PlanInfo(Guid Id, string Title, string Content, int Version,
        DateTime UpdatedAt, string Status);
}
```

## Step 2: Wire `SpecViewerScreen` into `CardDetailScreen`

Update `CardDetailScreen.HandleKeyAsync` — replace the `s` placeholder:
```csharp
case ConsoleKey.S:
    var mode = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("View:")
            .AddChoices("Specs", "Plans"));
    var specScreen = new SpecViewerScreen(
        _apiClientFactory, _appState, _errorCollector,
        _projectId, _cardId,
        mode == "Specs" ? "spec" : "plan");
    _appState.CurrentScreen = specScreen;
    await specScreen.OnEnterAsync();
    await specScreen.RenderAsync();
    break;
```

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Screens/SpecViewerScreen.cs src/HydraForge.Tui/Screens/CardDetailScreen.cs
git commit -m "feat(tui): add spec/plan viewer and editor with $EDITOR support"
```