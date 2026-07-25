using HydraForge.Tui.Generated;
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
    private readonly string _mode;

    private List<DocumentItem> _documents = new();
    private int _selectedIndex;

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
        AnsiConsole.Write(new Rule($"[blue]{title}[/]"));

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
        AnsiConsole.Write(new Rule($"[blue]{Markup.Escape(doc.Title)}[/]"));

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

            if (_mode == "spec")
            {
                await client.SpecsPUTAsync(_projectId, doc.Id, new UpdateSpecRequest
                {
                    Title = doc.Title,
                    Description = null,
                    Content = newContent
                });
            }
            else
            {
                await client.PlansPUTAsync(_projectId, doc.Id, new UpdatePlanRequest
                {
                    Title = doc.Title,
                    Description = null,
                    Content = newContent
                });
            }

            AnsiConsole.MarkupLine("[green]Document updated.[/]");
            await LoadDocumentsAsync();
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
                await client.CardsPOST2Async(_projectId, _cardId, new CreateSpecRequest
                {
                    DocType = Enum.Parse<DocType>(docType),
                    Title = title,
                    Description = null,
                    Content = content
                });
            }
            else
            {
                await client.CardsPOSTAsync(_projectId, _cardId, new CreatePlanRequest
                {
                    Title = title,
                    Description = null,
                    Content = content,
                    SpecId = null,
                    Position = _documents.Count
                });
            }

            AnsiConsole.MarkupLine($"[green]{_mode} created![/]");
            await LoadDocumentsAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Create failed: {ex.Message}");
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

            if (_mode == "spec")
            {
                var list = await client.CardsGET2Async(_projectId, _cardId);
                _documents = list.Specs.Select(s => new DocumentItem(
                    s.Id, s.Title, s.Content, s.Version, s.UpdatedAt.DateTime, s.DocType.ToString(), null
                )).ToList();
            }
            else
            {
                var list = await client.CardsGETAsync(_projectId, _cardId);
                _documents = list.Plans.Select(p => new DocumentItem(
                    p.Id, p.Title, p.Content, p.Version, p.UpdatedAt.DateTime, null, p.Status.ToString()
                )).ToList();
            }
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Load error: {ex.Message}");
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
}
