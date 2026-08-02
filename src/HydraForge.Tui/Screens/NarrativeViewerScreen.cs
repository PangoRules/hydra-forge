using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class NarrativeViewerScreen(
    ApiClientFactory apiClientFactory,
    AppState appState,
    ErrorCollector errorCollector,
    Guid projectId
) : IScreen
{
    private string? _narrative;
    private DateTimeOffset? _generatedAt;
    private int _scroll;

    public async Task OnEnterAsync()
    {
        try
        {
            var client = apiClientFactory.GetClient();
            var snapshot = await client.ProjectSnapshotAsync(projectId);
            _narrative = snapshot.AiNarrative;
            _generatedAt = snapshot.AiNarrativeGeneratedAt;
        }
        catch (ApiException ex)
        {
            errorCollector.Add("N/A", $"Failed to load AI narrative: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            errorCollector.Add("N/A", $"Connection error: {ex.Message}");
        }
    }

    public Task OnExitAsync() => Task.CompletedTask;

    public Task RenderAsync()
    {
        AnsiConsole.Clear();
        ConsoleSize.Sync();

        AnsiConsole.Write(new Rule("[blue]AI Project Narrative[/]"));

        if (string.IsNullOrWhiteSpace(_narrative))
        {
            var empty = new Panel(
                new Markup(
                    "[grey]No AI narrative has been generated for this project yet. Narratives are generated nightly.[/]"
                )
            )
            {
                Border = BoxBorder.Rounded,
                Expand = true,
            };
            AnsiConsole.Write(empty);
            KeyHintBar.Render(["[Esc] Back", "[q] Quit"]);
            return Task.CompletedTask;
        }

        var lines = _narrative.Replace("\r\n", "\n").Split('\n');
        var pageSize = Math.Max(5, AnsiConsole.Profile.Height - 5);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, lines.Length - pageSize));
        var visible = string.Join("\n", lines.Skip(_scroll).Take(pageSize));

        var header = _generatedAt.HasValue
            ? $" Generated {DateFormatting.FormatTimestamp(_generatedAt.Value.DateTime)} "
            : " Narrative ";

        var panel = new Panel(new Markup(Markup.Escape(visible)))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(header),
            Expand = true,
        };
        AnsiConsole.Write(panel);
        KeyHintBar.Render(["[j/k] Scroll", "[Esc] Back", "[q] Quit", "[?] Help"]);

        return Task.CompletedTask;
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.J or ConsoleKey.DownArrow:
                _scroll++;
                await RenderAsync();
                break;

            case ConsoleKey.K
            or ConsoleKey.UpArrow:
                _scroll = Math.Max(0, _scroll - 1);
                await RenderAsync();
                break;

            case ConsoleKey.Escape:
                appState.CurrentScreen = null;
                break;

            case ConsoleKey.Q:
                if (QuitConfirm.Show())
                    Environment.Exit(0);
                break;

            case ConsoleKey when key.KeyChar == '?':
                ShowHelp();
                await RenderAsync();
                break;
        }
    }

    private static void ShowHelp() =>
        HelpOverlay.Show(
            "AI Narrative",
            [("j/k, ↑/↓", "Scroll"), ("Esc", "Back to board"), ("q", "Quit"), ("?", "This help")]
        );
}
