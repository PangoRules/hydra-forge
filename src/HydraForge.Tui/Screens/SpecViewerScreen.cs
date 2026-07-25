using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace HydraForge.Tui.Screens;

public class SpecViewerScreen : IScreen
{
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private readonly EditorLauncher _editorLauncher = new();

    private readonly Guid _projectId;
    private readonly Guid _cardId;
    private readonly CardType _cardType;
    private readonly string _mode;

    private List<DocumentItem> _documents = new();
    private List<VersionItem> _versions = new();
    private int _selectedIndex;

    public SpecViewerScreen(
        ApiClientFactory apiClientFactory,
        AppState appState,
        ErrorCollector errorCollector,
        Guid projectId,
        Guid cardId,
        CardType cardType,
        string mode = "spec")
    {
        _apiClientFactory = apiClientFactory;
        _appState = appState;
        _errorCollector = errorCollector;
        _projectId = projectId;
        _cardId = cardId;
        _cardType = cardType;
        _mode = mode;
    }

    public async Task OnEnterAsync()
    {
        await LoadDocumentsAsync();
        await LoadVersionsForSelectedAsync();
    }

    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        AnsiConsole.Clear();

        var title = _mode == "spec" ? "Specifications" : "Plans";
        AnsiConsole.Write(new Rule($"[blue]{title}[/]"));

        if (_documents.Count == 0)
        {
            var empty = new Panel(new Markup($"[grey]No {_mode}s for this card.[/]"))
            {
                Header = new PanelHeader(" Documents "),
                Border = BoxBorder.Rounded,
                Expand = true,
            };
            AnsiConsole.Write(empty);
            KeyHintBar.Render(BuildHints());
            return;
        }

        AnsiConsole.Write(BuildListPanel());
        AnsiConsole.Write(BuildDetailSplit());
        KeyHintBar.Render(BuildHints());
    }

    private Panel BuildListPanel()
    {
        var rows = new List<IRenderable>();
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

            rows.Add(new Markup($"{prefix} [bold]{Markup.Escape(doc.Title)}[/]{typeBadge}{statusBadge}"));
            rows.Add(new Markup($"   [grey]v{doc.Version} — {doc.UpdatedAt:yyyy-MM-dd HH:mm}[/]"));
        }

        return new Panel(new Rows(rows))
        {
            Header = new PanelHeader($" Documents ({_documents.Count}) "),
            Border = BoxBorder.Rounded,
            Expand = true,
        };
    }

    private Grid BuildDetailSplit()
    {
        var doc = _documents[_selectedIndex];

        var width = Math.Max(20, AnsiConsole.Profile.Width);
        var historyWidth = Math.Clamp(width / 3, 24, 44);
        var previewWidth = width - historyWidth;

        // Cap growth at half the terminal height — a long doc with many
        // versions would otherwise push these panels past the visible screen.
        var maxHeight = Math.Max(10, AnsiConsole.Profile.Height / 2);

        // Table/Grid auto-sizing (Expand + a fixed-width column) doesn't add up
        // to the exact console width — setting Panel.Width directly on both
        // panels and concatenating with zero column padding does.
        var previewPanel = new Panel(new Markup(Markup.Escape(doc.Content)))
        {
            Header = new PanelHeader($" Preview: {Markup.Escape(doc.Title)} v{doc.Version} "),
            Border = BoxBorder.Rounded,
            Width = previewWidth,
            Height = maxHeight,
        };

        var historyPanel = new Panel(BuildHistoryContent(maxHeight))
        {
            Header = new PanelHeader(" History "),
            Border = BoxBorder.Rounded,
            Width = historyWidth,
            Height = maxHeight,
        };

        var grid = new Grid();
        grid.AddColumn(new GridColumn().PadRight(0));
        grid.AddColumn(new GridColumn().PadLeft(0));
        grid.AddRow(previewPanel, historyPanel);
        return grid;
    }

    // Each entry renders as 2 lines — slice to whole entries that fit rather
    // than letting the panel hard-crop mid-entry. [r] Restore still lists the
    // full history regardless of what's visible here.
    private IRenderable BuildHistoryContent(int maxHeight)
    {
        if (_versions.Count == 0)
            return new Markup("[grey]No history yet.[/]");

        var maxEntries = Math.Max(1, (maxHeight - 2) / 2);
        var visible = _versions.Take(maxEntries).ToList();

        var rows = visible
            .Select(v => (IRenderable)new Markup(
                $"v{v.Version} [grey]{v.CreatedAt:yyyy-MM-dd HH:mm}[/]\n" +
                $"[grey]{Markup.Escape(v.CreatedByUserId.ToString()[..8])}[/]"
            ))
            .ToList();

        if (visible.Count < _versions.Count)
            rows.Add(new Markup($"[grey]+{_versions.Count - visible.Count} more — [[r]] Restore[/]"));

        return new Rows(rows);
    }

    // Hints reflect what's actually reachable right now — e.g. [c] Create only
    // shows once (Spec.CardId is max 1 per Card, see D-44), [s] Status only for Plans.
    private IEnumerable<string> BuildHints()
    {
        if (_documents.Count > 0)
        {
            yield return "[j/k] Move";
            yield return "[Enter] View";
            yield return "[e] Edit";
            if (_mode == "plan")
                yield return "[s] Status";
            if (_versions.Count > 1)
                yield return "[r] Restore version";
        }
        if (CanCreateMore)
            yield return "[c] Create";
        yield return "[Esc] Back";
        yield return "[q] Quit";
    }

    // Spec.CardId owns max 1 Spec per Card (see D-44) — Plans are legitimately
    // multi, so this only restricts creation once a card already has its Spec.
    private bool CanCreateMore => _mode != "spec" || _documents.Count == 0;

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (_selectedIndex < _documents.Count - 1)
                {
                    _selectedIndex++;
                    await LoadVersionsForSelectedAsync();
                }
                await RenderAsync();
                break;

            case ConsoleKey.K or ConsoleKey.UpArrow:
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                    await LoadVersionsForSelectedAsync();
                }
                await RenderAsync();
                break;

            case ConsoleKey.Enter:
                await ViewDocumentAsync();
                break;

            case ConsoleKey.E:
                await EditDocumentAsync();
                break;

            case ConsoleKey.C:
                if (CanCreateMore)
                    await CreateDocumentAsync();
                break;

            case ConsoleKey.S:
                if (_mode == "plan")
                    await ChangeStatusAsync();
                break;

            case ConsoleKey.R:
                await RestoreVersionAsync();
                break;

            case ConsoleKey.Q:
                if (AnsiConsole.Confirm("Quit HydraForge?"))
                    Environment.Exit(0);
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
        var lines = doc.Content.Replace("\r\n", "\n").Split('\n');

        // Rule + panel header/border + hint line eat 4 rows; the rest is content.
        var pageSize = Math.Max(5, AnsiConsole.Profile.Height - 4);
        var scroll = 0;

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[blue]{Markup.Escape(doc.Title)}[/]"));

            var visible = string.Join("\n", lines.Skip(scroll).Take(pageSize));
            var panel = new Panel(new Markup(Markup.Escape(visible)))
            {
                Border = BoxBorder.Rounded,
                Header = new PanelHeader($" {_mode} v{doc.Version} "),
                Expand = true,
            };
            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("[grey][[j/k]] Scroll  [[Esc]] Back[/]");

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.J or ConsoleKey.DownArrow:
                    if (scroll + pageSize < lines.Length) scroll++;
                    break;
                case ConsoleKey.K or ConsoleKey.UpArrow:
                    if (scroll > 0) scroll--;
                    break;
                case ConsoleKey.Escape or ConsoleKey.Q or ConsoleKey.Enter:
                    await RenderAsync();
                    return;
            }
        }
    }

    private async Task EditDocumentAsync()
    {
        if (_selectedIndex >= _documents.Count) return;
        var doc = _documents[_selectedIndex];

        if (_mode == "plan" && doc.Status == "Done")
        {
            AnsiConsole.MarkupLine("[yellow]Cannot edit a completed plan.[/]");
            return;
        }

        var newTitle = AnsiConsole.Prompt(
            new TextPrompt<string>("Title:")
                .DefaultValue(doc.Title)
                .Validate(t => string.IsNullOrWhiteSpace(t)
                    ? ValidationResult.Error("Title required")
                    : ValidationResult.Success()));

        var newContent = await _editorLauncher.EditAsync(doc.Content);
        if (newContent == null)
        {
            AnsiConsole.MarkupLine("[yellow]Editor failed. Using inline prompt.[/]");
            newContent = AnsiConsole.Prompt(
                new TextPrompt<string>("Content:")
                    .DefaultValue(doc.Content));
        }

        if (newTitle == doc.Title && newContent == doc.Content)
        {
            AnsiConsole.MarkupLine("[grey]No changes.[/]");
            return;
        }

        try
        {
            var client = _apiClientFactory.GetClient();

            if (_mode == "spec")
            {
                await client.SpecsPUTAsync(_projectId, doc.Id, new UpdateSpecRequest
                {
                    Title = newTitle,
                    Description = doc.Description,
                    Content = newContent
                });
            }
            else
            {
                await client.PlansPUTAsync(_projectId, doc.Id, new UpdatePlanRequest
                {
                    Title = newTitle,
                    Description = doc.Description,
                    Content = newContent
                });
            }

            AnsiConsole.MarkupLine("[green]Document updated.[/]");
            await LoadDocumentsAsync();
            await LoadVersionsForSelectedAsync();
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

        var content = await _editorLauncher.EditAsync("");
        if (content == null)
        {
            AnsiConsole.MarkupLine("[yellow]Editor failed. Using inline prompt.[/]");
            content = AnsiConsole.Prompt(
                new TextPrompt<string>("Content (markdown):")
                    .DefaultValue(""));
        }

        try
        {
            var client = _apiClientFactory.GetClient();

            if (_mode == "spec")
            {
                await client.CardsPOST2Async(_projectId, _cardId, new CreateSpecRequest
                {
                    DocType = CardTypeMapper.ToDocType(_cardType),
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
            await LoadVersionsForSelectedAsync();
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

    private async Task ChangeStatusAsync()
    {
        if (_selectedIndex >= _documents.Count) return;
        var doc = _documents[_selectedIndex];
        if (doc.Status == null) return;

        var current = Enum.Parse<PlanStatus>(doc.Status);
        var choices = Enum.GetValues<PlanStatus>().Where(s => s != current).Select(s => s.ToString()).ToList();

        var choice = await ListPrompt.Show("New status:", choices, renderBackdrop: RenderAsync);
        if (choice == null) return;

        try
        {
            var client = _apiClientFactory.GetClient();
            await client.StatusAsync(_projectId, doc.Id, new SetPlanStatusRequest
            {
                Status = Enum.Parse<PlanStatus>(choice)
            });

            AnsiConsole.MarkupLine($"[green]Status set to {choice}.[/]");
            await LoadDocumentsAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Status change failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task RestoreVersionAsync()
    {
        if (_selectedIndex >= _documents.Count || _versions.Count <= 1) return;
        var doc = _documents[_selectedIndex];

        if (_mode == "plan" && doc.Status == "Done")
        {
            AnsiConsole.MarkupLine("[yellow]Cannot restore a completed plan.[/]");
            return;
        }

        var choices = _versions.Select(v => $"v{v.Version} — {v.CreatedAt:yyyy-MM-dd HH:mm}").ToList();
        var choice = await ListPrompt.Show("Restore version:", choices, renderBackdrop: RenderAsync);
        if (choice == null) return;

        var version = int.Parse(choice[1..choice.IndexOf(' ')]);

        try
        {
            var client = _apiClientFactory.GetClient();

            if (_mode == "spec")
                await client.Restore3Async(_projectId, doc.Id, new RestoreSpecVersionRequest { Version = version });
            else
                await client.Restore2Async(_projectId, doc.Id, new RestorePlanVersionRequest { Version = version });

            AnsiConsole.MarkupLine($"[green]Restored v{version}.[/]");
            await LoadDocumentsAsync();
            await LoadVersionsForSelectedAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Restore failed: {ex.Message}");
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
                    s.Id, s.Title, s.Content, s.Version, s.UpdatedAt.DateTime, s.DocType.ToString(), null, s.Description
                )).ToList();
            }
            else
            {
                var list = await client.CardsGETAsync(_projectId, _cardId);
                _documents = list.Plans.Select(p => new DocumentItem(
                    p.Id, p.Title, p.Content, p.Version, p.UpdatedAt.DateTime, null, p.Status.ToString(), p.Description
                )).ToList();
            }

            if (_selectedIndex >= _documents.Count)
                _selectedIndex = Math.Max(0, _documents.Count - 1);
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

    private async Task LoadVersionsForSelectedAsync()
    {
        _versions = [];
        if (_selectedIndex >= _documents.Count) return;
        var doc = _documents[_selectedIndex];

        try
        {
            var client = _apiClientFactory.GetClient();

            if (_mode == "spec")
            {
                var list = await client.Versions2Async(_projectId, doc.Id);
                _versions = list.Versions
                    .Select(v => new VersionItem(v.Version, v.CreatedAt.DateTime, v.CreatedByUserId))
                    .OrderByDescending(v => v.Version)
                    .ToList();
            }
            else
            {
                var list = await client.VersionsAsync(_projectId, doc.Id);
                _versions = list.Versions
                    .Select(v => new VersionItem(v.Version, v.CreatedAt.DateTime, v.CreatedByUserId))
                    .OrderByDescending(v => v.Version)
                    .ToList();
            }
        }
        catch (ApiException ex)
        {
            _errorCollector.Add("N/A", $"Version load error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _errorCollector.Add("N/A", $"Connection error: {ex.Message}");
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
        DateTime UpdatedAt, string? DocType, string? Status, string? Description
    );

    private record VersionItem(int Version, DateTime CreatedAt, Guid CreatedByUserId);
}
