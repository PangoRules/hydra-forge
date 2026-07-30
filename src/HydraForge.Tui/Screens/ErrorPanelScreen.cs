using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class ErrorPanelScreen(AppState appState, ErrorCollector errorCollector) : IScreen
{
    private readonly AppState _appState = appState;
    private readonly ErrorCollector _errorCollector = errorCollector;
    private int _selectedIndex;

    public Task OnEnterAsync() => Task.CompletedTask;

    public Task OnExitAsync() => Task.CompletedTask;

    public Task RenderAsync()
    {
        AnsiConsole.Clear();

        var errors = _errorCollector.GetErrors();

        if (errors.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No errors.[/]");
            AnsiConsole.MarkupLine("[grey]Press Esc to close[/]");
            return Task.CompletedTask;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, errors.Count - 1);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("")
            .AddColumn("Time")
            .AddColumn("Correlation ID")
            .AddColumn("Message");

        for (var i = 0; i < errors.Count; i++)
        {
            var e = errors[i];
            var prefix = i == _selectedIndex ? "[blue]>[/]" : " ";
            var corrId =
                e.CorrelationId.Length > 12 ? e.CorrelationId[..12] + "..." : e.CorrelationId;

            table.AddRow(
                prefix,
                $"[grey]{e.Timestamp:HH:mm:ss}[/]",
                $"[grey]{Markup.Escape(corrId)}[/]",
                Markup.Escape(e.Message)
            );
        }

        var panel = new Panel(table)
        {
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(foreground: Color.Red),
            Header = new PanelHeader($" Errors ({errors.Count}) "),
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine(
            "[grey][[j/k]] Navigate  [[Del]] Dismiss  [[?]] Help  [[Esc]] Close[/]"
        );

        return Task.CompletedTask;
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        var errors = _errorCollector.GetErrors();

        switch (key.Key)
        {
            case ConsoleKey.J or ConsoleKey.DownArrow:
                if (errors.Count > 0)
                    _selectedIndex = (_selectedIndex + 1) % errors.Count;
                await RenderAsync();
                return;

            case ConsoleKey.K
            or ConsoleKey.UpArrow:
                if (errors.Count > 0)
                    _selectedIndex = (_selectedIndex - 1 + errors.Count) % errors.Count;
                await RenderAsync();
                return;

            case ConsoleKey.Delete:
                if (_selectedIndex < errors.Count)
                    _errorCollector.Dismiss(_selectedIndex);
                await RenderAsync();
                return;

            case ConsoleKey.Escape:
                _appState.CurrentScreen = null;
                return;

            case ConsoleKey when key.KeyChar == '?':
                ShowHelp();
                await RenderAsync();
                return;
        }
    }

    private static void ShowHelp() =>
        HelpOverlay.Show(
            "Errors",
            [
                ("j/k, ↑/↓", "Navigate"),
                ("Del", "Dismiss selected error"),
                ("Esc", "Back to board"),
                ("?", "This help"),
            ]
        );
}
