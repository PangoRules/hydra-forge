using Spectre.Console;

namespace HydraForge.Tui.Rendering;

/// <summary>
/// Full-screen modal listing who else is currently online on this project, and which
/// card (if any) they're looking at. Same blocking-prompt pattern as HelpOverlay.
/// </summary>
public static class OnlineUsersOverlay
{
    public static void Show(IReadOnlyList<(string Username, int? ViewingCardNumber)> users)
    {
        var table = new Table().Border(TableBorder.Rounded).HideHeaders().Expand();
        table.AddColumn(new TableColumn("Username").Width(24));
        table.AddColumn(new TableColumn("Viewing"));

        if (users.Count == 0)
        {
            table.AddRow("[grey]No one else is online[/]", "");
        }
        else
        {
            foreach (var (username, cardNumber) in users)
            {
                var viewing = cardNumber.HasValue ? $"[cyan1]Card #{cardNumber}[/]" : "[grey]—[/]";
                table.AddRow(Markup.Escape(username), viewing);
            }
        }

        var panel = new Panel(table)
        {
            Header = new PanelHeader(" Online Users "),
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(foreground: Color.Blue),
        };

        AnsiConsole.Clear();
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[grey]Press [[o]] or [[Esc]] to close[/]");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape || key.KeyChar is 'o' or 'O')
                break;
        }
    }
}
