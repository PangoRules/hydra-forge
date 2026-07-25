using Spectre.Console;

namespace HydraForge.Tui.Rendering;

/// <summary>
/// Full-screen modal listing every keybind for the current screen. Blocks on
/// Console.ReadKey until dismissed — same blocking-prompt pattern already used
/// by AnsiConsole.Prompt calls elsewhere in the screens, safe to call from
/// inside HandleKeyAsync.
/// </summary>
public static class HelpOverlay
{
    public static void Show(string title, IEnumerable<(string Key, string Description)> bindings)
    {
        var table = new Table().Border(TableBorder.Rounded).HideHeaders().Expand();
        table.AddColumn(new TableColumn("Key").Width(16));
        table.AddColumn(new TableColumn("Description"));

        foreach (var (key, description) in bindings)
            table.AddRow($"[cyan1]{Markup.Escape(key)}[/]", Markup.Escape(description));

        var panel = new Panel(table)
        {
            Header = new PanelHeader($" {title} — Keyboard Shortcuts "),
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(foreground: Color.Blue),
        };

        AnsiConsole.Clear();
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[grey]Press [[?]] or [[Esc]] to close[/]");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape || key.KeyChar == '?')
                break;
        }
    }
}
