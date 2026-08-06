using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace HydraForge.Tui.Screens;

public class ProjectDocumentsScreen(
    ApiClientFactory apiClientFactory,
    AppState appState,
    ErrorCollector errorCollector,
    Guid projectId
) : IScreen
{
    private readonly EditorLauncher _editorLauncher = new();
    private readonly SemaphoreSlim _renderLock = new(1, 1);

    private List<DocumentItem> _documents = [];
    private List<VersionItem> _versions = [];
    private int _selectedIndex;

    public async Task OnEnterAsync()
    {
        await LoadDocumentsAsync();
        await LoadVersionsForSelectedAsync();
    }

    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        await _renderLock.WaitAsync();
        try
        {
            AnsiConsole.Clear();
            ConsoleSize.Sync();

            AnsiConsole.Write(new Rule("[blue]Project Documents[/]"));

            if (_documents.Count == 0)
            {
                var empty = new Panel(new Markup("[grey]No project documents yet.[/]"))
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
        finally
        {
            _renderLock.Release();
        }
    }

    private Panel BuildListPanel()
    {
        var capHeight = Math.Max(8, AnsiConsole.Profile.Height / 4);
        var naturalHeight = 2 + _documents.Count * 2;
        var maxHeight = Math.Min(capHeight, naturalHeight);
        var maxVisible = Math.Max(1, (maxHeight - 2) / 2);
        var window = ListScrollWindow.Compute(_documents.Count, _selectedIndex, maxVisible);

        var rows = new List<IRenderable>();
        if (window.HasMoreAbove)
            rows.Add(new Markup($"[grey]↑ {window.Start} more above[/]"));

        for (int i = window.Start; i < window.End; i++)
        {
            var doc = _documents[i];
            var isSelected = i == _selectedIndex;
            var prefix = isSelected ? "[blue]>[/]" : " ";

            var typeBadge = $" [{GetTypeColor(doc.DocType)}]{doc.DocType}[/]";

            rows.Add(new Markup($"{prefix} [bold]{Markup.Escape(doc.Title)}[/]{typeBadge}"));
            rows.Add(
                new Markup(
                    $"   [grey]v{doc.Version} — {DateFormatting.FormatTimestamp(doc.UpdatedAt)}[/]"
                )
            );
        }

        if (window.HasMoreBelow)
            rows.Add(new Markup($"[grey]↓ {_documents.Count - window.End} more below[/]"));

        return new Panel(new Rows(rows))
        {
            Header = new PanelHeader($" Documents ({_documents.Count}) "),
            Border = BoxBorder.Rounded,
            Expand = true,
            Height = maxHeight,
        };
    }

    private Grid BuildDetailSplit()
    {
        var doc = _documents[_selectedIndex];

        var width = Math.Max(20, AnsiConsole.Profile.Width);
        var historyWidth = Math.Clamp(width / 3, 24, 44);
        var previewWidth = width - historyWidth;

        var maxHeight = Math.Max(10, AnsiConsole.Profile.Height / 2);

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

    private IRenderable BuildHistoryContent(int maxHeight)
    {
        if (_versions.Count == 0)
            return new Markup("[grey]No history yet.[/]");

        var maxEntries = Math.Max(1, (maxHeight - 2) / 2);
        var visible = _versions.Take(maxEntries).ToList();

        var rows = visible
            .Select(v =>
                (IRenderable)
                    new Markup(
                        $"v{v.Version} [grey]{DateFormatting.FormatTimestamp(v.CreatedAt)}[/]\n"
                            + $"[grey]{Markup.Escape(v.CreatedByUserId.ToString()[..8])}[/]"
                    )
            )
            .ToList();

        if (visible.Count < _versions.Count)
            rows.Add(
                new Markup($"[grey]+{_versions.Count - visible.Count} more — [[r]] Restore[/]")
            );

        return new Rows(rows);
    }

    private IEnumerable<string> BuildHints()
    {
        if (_documents.Count > 0)
        {
            yield return "[j/k] Move";
            yield return "[Enter] View";
            yield return "[e] Edit";
            if (_versions.Count > 1)
                yield return "[r] Restore version";
            yield return "[x] Export";
        }
        yield return "[c] Create";
        yield return "[Esc] Back";
        yield return "[q] Quit";
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (_selectedIndex < _documents.Count - 1)
                {
                    _selectedIndex++;
                    await LoadVersionsForSelectedAsync();
                    await RenderAsync();
                }
                break;

            case ConsoleKey.K
            or ConsoleKey.UpArrow:
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                    await LoadVersionsForSelectedAsync();
                    await RenderAsync();
                }
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

            case ConsoleKey.R:
                await RestoreVersionAsync();
                break;

            case ConsoleKey.X:
                await ExportDocumentAsync();
                break;

            case ConsoleKey.Q:
                if (QuitConfirm.Show())
                    Environment.Exit(0);
                break;

            case ConsoleKey.Escape:
                appState.CurrentScreen = null;
                break;
        }
    }

    private async Task ViewDocumentAsync()
    {
        if (_selectedIndex >= _documents.Count)
            return;
        var doc = _documents[_selectedIndex];
        var lines = doc.Content.Replace("\r\n", "\n").Split('\n');

        var pageSize = Math.Max(5, AnsiConsole.Profile.Height - 4);
        var scroll = 0;

        while (true)
        {
            AnsiConsole.Clear();
            ConsoleSize.Sync();
            AnsiConsole.Write(new Rule($"[blue]{Markup.Escape(doc.Title)}[/]"));

            var visible = string.Join("\n", lines.Skip(scroll).Take(pageSize));
            var panel = new Panel(new Markup(Markup.Escape(visible)))
            {
                Border = BoxBorder.Rounded,
                Header = new PanelHeader($" Document v{doc.Version} "),
                Expand = true,
                Height = pageSize + 2,
            };
            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("[grey][[j/k]] Scroll  [[Esc]] Back[/]");

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.J or ConsoleKey.DownArrow:
                    if (scroll + pageSize < lines.Length)
                        scroll++;
                    break;
                case ConsoleKey.K or ConsoleKey.UpArrow:
                    if (scroll > 0)
                        scroll--;
                    break;
                case ConsoleKey.Escape or ConsoleKey.Q or ConsoleKey.Enter:
                    await RenderAsync();
                    return;
            }
        }
    }

    private async Task EditDocumentAsync()
    {
        if (_selectedIndex >= _documents.Count)
            return;
        var doc = _documents[_selectedIndex];

        var newTitle = AnsiConsole.Prompt(
            new TextPrompt<string>("Title:")
                .DefaultValue(doc.Title)
                .Validate(t =>
                    string.IsNullOrWhiteSpace(t)
                        ? ValidationResult.Error("Title required")
                        : ValidationResult.Success()
                )
        );

        var newContent = await _editorLauncher.EditAsync(doc.Content);
        if (newContent == null)
        {
            AnsiConsole.MarkupLine("[yellow]Editor failed. Using inline prompt.[/]");
            newContent = AnsiConsole.Prompt(
                new TextPrompt<string>("Content:").DefaultValue(doc.Content)
            );
        }

        if (newTitle == doc.Title && newContent == doc.Content)
        {
            AnsiConsole.MarkupLine("[grey]No changes.[/]");
            return;
        }

        try
        {
            var client = apiClientFactory.GetClient();
            await client.ProjectDocumentsPUTAsync(
                projectId,
                doc.Id,
                new UpdateProjectDocumentRequest
                {
                    Title = newTitle,
                    Description = doc.Description,
                    Content = newContent,
                }
            );

            AnsiConsole.MarkupLine("[green]Document updated.[/]");
            await LoadDocumentsAsync();
            await LoadVersionsForSelectedAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            errorCollector.Add("N/A", $"Update failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task CreateDocumentAsync()
    {
        var docTypes = Enum.GetValues<ProjectDocType>().Select(d => d.ToString()).ToList();
        var typeIdx = await ListPrompt.Show(
            "Document type:",
            docTypes,
            renderBackdrop: RenderAsync
        );
        if (!typeIdx.HasValue)
            return;

        var docType = Enum.Parse<ProjectDocType>(docTypes[typeIdx.Value]);

        var title = AnsiConsole.Prompt(
            new TextPrompt<string>("Title:").Validate(t =>
                string.IsNullOrWhiteSpace(t)
                    ? ValidationResult.Error("Title required")
                    : ValidationResult.Success()
            )
        );

        var content = await _editorLauncher.EditAsync("");
        if (content == null)
        {
            AnsiConsole.MarkupLine("[yellow]Editor failed. Using inline prompt.[/]");
            content = AnsiConsole.Prompt(
                new TextPrompt<string>("Content (markdown):").DefaultValue("")
            );
        }

        try
        {
            var client = apiClientFactory.GetClient();
            await client.ProjectDocumentsPOSTAsync(
                projectId,
                new CreateProjectDocumentRequest
                {
                    DocType = docType,
                    Title = title,
                    Description = null,
                    Content = content,
                }
            );

            AnsiConsole.MarkupLine("[green]Document created![/]");
            await LoadDocumentsAsync();
            await LoadVersionsForSelectedAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            errorCollector.Add("N/A", $"Create failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task RestoreVersionAsync()
    {
        if (_selectedIndex >= _documents.Count || _versions.Count <= 1)
            return;
        var doc = _documents[_selectedIndex];

        var choices = _versions
            .Select(v => $"v{v.Version} — {DateFormatting.FormatTimestamp(v.CreatedAt)}")
            .ToList();
        var idx = await ListPrompt.Show("Restore version:", choices, renderBackdrop: RenderAsync);
        if (!idx.HasValue)
            return;

        var version = _versions[idx.Value].Version;

        try
        {
            var client = apiClientFactory.GetClient();
            await client.Restore3Async(
                projectId,
                doc.Id,
                new RestoreProjectDocumentVersionRequest { Version = version }
            );

            AnsiConsole.MarkupLine($"[green]Restored v{version}.[/]");
            await LoadDocumentsAsync();
            await LoadVersionsForSelectedAsync();
            await RenderAsync();
        }
        catch (ApiException ex)
        {
            errorCollector.Add("N/A", $"Restore failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private async Task ExportDocumentAsync()
    {
        if (_selectedIndex >= _documents.Count)
            return;
        var doc = _documents[_selectedIndex];

        try
        {
            var cwd = Environment.CurrentDirectory;
            var targetDir = Path.Combine(cwd, "docs", "exports");
            Directory.CreateDirectory(targetDir);
            var path = Path.Combine(targetDir, $"{Slugify(doc.Title)}.md");
            await File.WriteAllTextAsync(path, doc.Content);

            AnsiConsole.MarkupLine($"[green]Exported to {Markup.Escape(path)}[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
            await RenderAsync();
        }
        catch (IOException ex)
        {
            errorCollector.Add("N/A", $"Export failed: {ex.Message}");
        }
    }

    private static string Slugify(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var slug = System.Text.RegularExpressions.Regex.Replace(lower, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "untitled" : slug;
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            var client = apiClientFactory.GetClient();
            var list = await client.ProjectDocumentsGETAsync(projectId);
            _documents =
            [
                .. list.Documents.Select(d => new DocumentItem(
                    d.Id,
                    d.Title,
                    d.Content,
                    d.Version,
                    d.UpdatedAt.DateTime,
                    d.DocType.ToString(),
                    d.Description
                )),
            ];

            if (_selectedIndex >= _documents.Count)
                _selectedIndex = Math.Max(0, _documents.Count - 1);
        }
        catch (ApiException ex)
        {
            errorCollector.Add("N/A", $"Load error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            errorCollector.Add("N/A", $"Load error: {ex.Message}");
        }
    }

    private async Task LoadVersionsForSelectedAsync()
    {
        _versions = [];
        if (_selectedIndex >= _documents.Count)
            return;
        var doc = _documents[_selectedIndex];

        try
        {
            var client = apiClientFactory.GetClient();
            var list = await client.Versions2Async(projectId, doc.Id);
            _versions =
            [
                .. list
                    .Versions.Select(v => new VersionItem(
                        v.Version,
                        v.CreatedAt.DateTime,
                        v.CreatedByUserId
                    ))
                    .OrderByDescending(v => v.Version),
            ];
        }
        catch (ApiException ex)
        {
            errorCollector.Add("N/A", $"Version load error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    private static string GetTypeColor(string type) =>
        type switch
        {
            "Scope" => "blue",
            "Glossary" => "cyan",
            "DataModel" => "yellow",
            "Architecture" => "green",
            "FunctionalSpec" => "blue",
            "Decisions" => "magenta",
            "Reference" => "grey",
            _ => "grey",
        };

    private record DocumentItem(
        Guid Id,
        string Title,
        string Content,
        int Version,
        DateTime UpdatedAt,
        string DocType,
        string? Description
    );

    private record VersionItem(int Version, DateTime CreatedAt, Guid CreatedByUserId);
}
