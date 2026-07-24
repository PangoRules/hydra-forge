# Plan 11: Dependency Panel

**Branch:** `task/tui-dependency-panel`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 11

**Goal:** Modal dependency panel: search card by number, select relationship type, confirm. Triggered by `d` key from board or card detail.

**Depends on:** Task 3 (ApiClientFactory), Task 9 (CardDetailScreen).

---

## Step 1: Create `DependencyPanel` modal screen

Create `src/HydraForge.Tui/Screens/DependencyPanel.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class DependencyPanel : IScreen
{
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private readonly Guid _projectId;
    private readonly Guid _sourceCardId;

    private string _searchText = "";
    private string _selectedType = "BlockedBy";
    private int _focusIndex; // 0=search, 1=type, 2=confirm
    private List<CardSearchResult> _searchResults = new();

    private static readonly string[] RelationshipTypes = ["BlockedBy", "Precedes", "Relates", "SpawnedFrom"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DependencyPanel(
        ApiClientFactory apiClientFactory,
        AppState appState,
        ErrorCollector errorCollector,
        Guid projectId,
        Guid sourceCardId)
    {
        _apiClientFactory = apiClientFactory;
        _appState = appState;
        _errorCollector = errorCollector;
        _projectId = projectId;
        _sourceCardId = sourceCardId;
    }

    public Task OnEnterAsync() => Task.CompletedTask;
    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        AnsiConsole.Clear();

        var panel = new Panel(
            new Rows(
                new Markup($"[bold]Add Dependency[/]"),
                new Rule(),
                BuildSearchSection(),
                new Rule(),
                BuildTypeSection(),
                new Rule(),
                BuildConfirmSection()
            )
        )
        {
            Border = BoxBorder.Heavy,
            BorderColor = Color.Blue,
            Header = new PanelHeader(" Dependency Panel "),
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[grey][[Tab]] Switch focus  [[Enter]] Confirm  [[Esc]] Cancel[/]");
    }

    private IRenderable BuildSearchSection()
    {
        var border = _focusIndex == 0 ? Color.Blue : Color.Grey;
        var label = _focusIndex == 0 ? "[blue bold]Card Number/ID:[/]" : "[grey]Card Number/ID:[/]";

        var content = new Rows(
            new Markup($"{label} {Markup.Escape(_searchText)}_"),
            new Rule().RuleStyle(Style.Plain)
        );

        if (_searchResults.Count > 0)
        {
            var results = _searchResults.Select(r =>
                new Markup($"  #{r.CardNumber} [grey]{Markup.Escape(r.Title)}[/]"));
            content = new Rows(new List<IRenderable> { content }
                .Concat(results.Cast<IRenderable>()).ToList());
        }

        return new Panel(content)
        {
            Border = BoxBorder.Rounded,
            BorderColor = border,
            Header = new PanelHeader(" Search "),
        };
    }

    private IRenderable BuildTypeSection()
    {
        var border = _focusIndex == 1 ? Color.Blue : Color.Grey;
        var items = RelationshipTypes.Select(t =>
        {
            var isSelected = t == _selectedType;
            var prefix = isSelected ? "[blue]>[/]" : " ";
            return new Markup($"{prefix} {t}");
        });

        return new Panel(new Rows(items.Cast<IRenderable>().ToList()))
        {
            Border = BoxBorder.Rounded,
            BorderColor = border,
            Header = new PanelHeader(" Relationship Type "),
        };
    }

    private IRenderable BuildConfirmSection()
    {
        var border = _focusIndex == 2 ? Color.Blue : Color.Grey;
        var text = _focusIndex == 2
            ? "[blue bold][[ Confirm ]][/]"
            : "[grey][[ Confirm ]][/]";

        return new Panel(new Markup(text))
        {
            Border = BoxBorder.Rounded,
            BorderColor = border,
        };
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Tab:
                _focusIndex = (_focusIndex + 1) % 3;
                await RenderAsync();
                break;

            case ConsoleKey.Tab when key.Modifiers == ConsoleModifiers.Shift:
                _focusIndex = (_focusIndex + 2) % 3;
                await RenderAsync();
                break;

            case ConsoleKey.Enter:
                if (_focusIndex == 2)
                    await ConfirmAsync();
                break;

            case ConsoleKey.Escape:
                _appState.CurrentScreen = null; // Dismiss modal
                break;

            case ConsoleKey.Backspace:
                if (_focusIndex == 0 && _searchText.Length > 0)
                {
                    _searchText = _searchText[..^1];
                    await SearchCardsAsync();
                    await RenderAsync();
                }
                break;

            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (_focusIndex == 1)
                {
                    var idx = Array.IndexOf(RelationshipTypes, _selectedType);
                    idx = (idx + 1) % RelationshipTypes.Length;
                    _selectedType = RelationshipTypes[idx];
                    await RenderAsync();
                }
                break;

            case ConsoleKey.K or ConsoleKey.UpArrow:
                if (_focusIndex == 1)
                {
                    var idx = Array.IndexOf(RelationshipTypes, _selectedType);
                    idx = (idx + RelationshipTypes.Length - 1) % RelationshipTypes.Length;
                    _selectedType = RelationshipTypes[idx];
                    await RenderAsync();
                }
                break;

            default:
                if (_focusIndex == 0 && !char.IsControl(key.KeyChar))
                {
                    _searchText += key.KeyChar;
                    await SearchCardsAsync();
                    await RenderAsync();
                }
                break;
        }
    }

    private async Task SearchCardsAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            _searchResults.Clear();
            return;
        }

        try
        {
            var client = _apiClientFactory.GetClient();
            var response = await client.GetAsync(
                $"api/projects/{_projectId}/cards?search={Uri.EscapeDataString(_searchText)}&take=5");

            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<CardSearchList>(JsonOptions);
                _searchResults = list?.Cards
                    .Where(c => c.Id != _sourceCardId)
                    .Select(c => new CardSearchResult(c.Id, c.CardNumber, c.Title))
                    .ToList() ?? new();
            }
        }
        catch { }
    }

    private async Task ConfirmAsync()
    {
        if (_searchResults.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No card selected. Type a card number first.[/]");
            return;
        }

        var targetCard = _searchResults[0];
        var type = _selectedType switch
        {
            "BlockedBy" => "BlockedBy",
            "Precedes" => "Precedes",
            "Relates" => "Relates",
            "SpawnedFrom" => "SpawnedFrom",
            _ => "Relates"
        };

        try
        {
            var client = _apiClientFactory.GetClient();
            var payload = new { targetCardId = targetCard.Id, type };

            var response = await client.PostAsJsonAsync(
                $"api/projects/{_projectId}/cards/{_sourceCardId}/relationships",
                payload, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine($"[green]Dependency added: {type} #{targetCard.CardNumber}[/]");
                _appState.CurrentScreen = null; // Dismiss
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _errorCollector.Add("N/A", $"Dependency error: {error}");
            }
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private record CardSearchResult(Guid Id, int CardNumber, string Title);
    private record CardSearchList(List<CardSearchResult> Cards);
}
```

## Step 2: Wire `DependencyPanel` into `BoardScreen` and `CardDetailScreen`

In `BoardScreen.HandleKeyAsync`, replace the `d` placeholder:
```csharp
case ConsoleKey.D:
    if (_columns.Count > 0)
    {
        var col = _columns[_selectedColumn];
        if (_selectedCard < col.Cards.Count)
        {
            var card = col.Cards[_selectedCard];
            var depPanel = new DependencyPanel(
                _apiClientFactory, _appState, _errorCollector,
                _projectId, card.Id);
            _appState.CurrentScreen = depPanel;
            await depPanel.RenderAsync();
        }
    }
    break;
```

In `CardDetailScreen.HandleKeyAsync`, replace the `d` placeholder:
```csharp
case ConsoleKey.D:
    var depPanel = new DependencyPanel(
        _apiClientFactory, _appState, _errorCollector,
        _projectId, _cardId);
    _appState.CurrentScreen = depPanel;
    await depPanel.RenderAsync();
    break;
```

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Screens/DependencyPanel.cs src/HydraForge.Tui/Screens/BoardScreen.cs src/HydraForge.Tui/Screens/CardDetailScreen.cs
git commit -m "feat(tui): add dependency panel with card search and type selection"
```