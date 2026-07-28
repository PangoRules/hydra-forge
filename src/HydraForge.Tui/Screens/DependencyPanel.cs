using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace HydraForge.Tui.Screens;

public class DependencyPanel(
    ApiClientFactory apiClientFactory,
    AppState appState,
    ErrorCollector errorCollector,
    Guid projectId,
    Guid sourceCardId
) : IScreen
{
    private readonly ApiClientFactory _apiClientFactory = apiClientFactory;
    private readonly AppState _appState = appState;
    private readonly ErrorCollector _errorCollector = errorCollector;
    private readonly Guid _projectId = projectId;
    private readonly Guid _sourceCardId = sourceCardId;
    private HydraForgeApiClient Client => _apiClientFactory.GetClient();

    private string _searchText = "";
    private string _selectedType = "BlockedBy";
    private int _focusIndex; // 0=search, 1=type, 2=confirm
    private int _resultCursor; // highlighted search result index
    private List<CardSearchResult> _searchResults = [];

    private static readonly string[] RelationshipTypes =
    [
        "BlockedBy",
        "Precedes",
        "Relates",
        "SpawnedFrom",
    ];

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
            BorderStyle = new Style(foreground: Color.Blue),
            Header = new PanelHeader(" Dependency Panel "),
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[grey][[Tab]] Switch focus  [[Enter]] Confirm  [[Esc]] Cancel[/]");
    }

    private Panel BuildSearchSection()
    {
        var borderColor = _focusIndex == 0 ? Color.Blue : Color.Grey;
        var label = _focusIndex == 0 ? "[blue bold]Card Number/ID:[/]" : "[grey]Card Number/ID:[/]";

        var content = new Rows(new Markup($"{label} {Markup.Escape(_searchText)}_"), new Rule());

        if (_searchResults.Count > 0)
        {
            var results = _searchResults.Select(
                (r, i) =>
                {
                    var isCursor = i == _resultCursor;
                    var cursorStr = isCursor ? "[blue]>[/] " : "  ";
                    return new Markup(
                        $"{cursorStr}#{r.CardNumber} [grey]{Markup.Escape(r.Title)}[/]"
                    );
                }
            );
            content = new Rows(
                new List<IRenderable> { content }.Concat([.. results.Cast<IRenderable>()])
            );
        }

        return new Panel(content)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: borderColor),
            Header = new PanelHeader(" Search "),
        };
    }

    private Panel BuildTypeSection()
    {
        var borderColor = _focusIndex == 1 ? Color.Blue : Color.Grey;
        var items = RelationshipTypes.Select(t =>
        {
            var isSelected = t == _selectedType;
            var prefix = isSelected ? "[blue]>[/]" : " ";
            return new Markup($"{prefix} {t}");
        });

        return new Panel(new Rows(items.Cast<IRenderable>().ToList()))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: borderColor),
            Header = new PanelHeader(" Relationship Type "),
        };
    }

    private Panel BuildConfirmSection()
    {
        var borderColor = _focusIndex == 2 ? Color.Blue : Color.Grey;
        var text = _focusIndex == 2 ? "[blue bold][[ Confirm ]][/]" : "[grey][[ Confirm ]][/]";

        return new Panel(new Markup(text))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: borderColor),
        };
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Tab when key.Modifiers == ConsoleModifiers.Shift:
                _focusIndex = (_focusIndex + 2) % 3;
                await RenderAsync();
                break;

            case ConsoleKey.Tab:
                _focusIndex = (_focusIndex + 1) % 3;
                await RenderAsync();
                break;

            case ConsoleKey.Enter:
                if (_focusIndex == 0 && _searchResults.Count > 0)
                {
                    await ConfirmAsync();
                }
                else if (_focusIndex == 2)
                {
                    await ConfirmAsync();
                }
                break;

            case ConsoleKey.Escape:
                _appState.CurrentScreen = null;
                break;

            case ConsoleKey.Backspace:
                if (_focusIndex == 0 && _searchText.Length > 0)
                {
                    _searchText = _searchText[..^1];
                    await SearchCardsAsync();
                    _resultCursor = 0;
                    await RenderAsync();
                }
                break;

            case ConsoleKey.J
            or ConsoleKey.DownArrow:
                if (_focusIndex == 0 && _searchResults.Count > 0)
                {
                    _resultCursor = (_resultCursor + 1) % _searchResults.Count;
                    await RenderAsync();
                }
                else if (_focusIndex == 1)
                {
                    var idx = Array.IndexOf(RelationshipTypes, _selectedType);
                    idx = (idx + 1) % RelationshipTypes.Length;
                    _selectedType = RelationshipTypes[idx];
                    await RenderAsync();
                }
                break;

            case ConsoleKey.K
            or ConsoleKey.UpArrow:
                if (_focusIndex == 0 && _searchResults.Count > 0)
                {
                    _resultCursor =
                        (_resultCursor + _searchResults.Count - 1) % _searchResults.Count;
                    await RenderAsync();
                }
                else if (_focusIndex == 1)
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
                    _resultCursor = 0;
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
            _resultCursor = 0;
            return;
        }

        try
        {
            var list = await Client.CardsGETAsync(_projectId, search: _searchText);
            _searchResults =
                list?.Cards.Where(c => c.Id != _sourceCardId)
                    .Select(c => new CardSearchResult(c.Id, c.CardNumber, c.Title))
                    .Take(5)
                    .ToList()
                ?? [];
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Search error: {ex.Message}");
            _searchResults.Clear();
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
            _searchResults.Clear();
        }
    }

    private async Task ConfirmAsync()
    {
        if (_searchResults.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No card selected. Type a card number first.[/]");
            return;
        }

        var targetCard = _searchResults[_resultCursor];

        try
        {
            var relType = Enum.Parse<RelationshipType>(_selectedType);

            await Client.CardRelationshipsPOSTAsync(
                _projectId,
                _sourceCardId,
                new CreateRelationshipRequest { TargetCardId = targetCard.Id, Type = relType }
            );

            AnsiConsole.MarkupLine($"[green]Dependency added: {_selectedType} #{targetCard.CardNumber}[/]");
            _appState.CurrentScreen = null;
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Dependency error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private record CardSearchResult(Guid Id, int CardNumber, string Title);
}
