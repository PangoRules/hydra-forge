using Spectre.Console;

namespace HydraForge.Tui.Rendering;

// Spectre's SelectionPrompt owns its own blocking read loop and only recognizes
// its hardcoded Up/Down/PageUp/PageDown/Enter keys -- there is no hook to extend
// it with j/k or Ctrl+N/Ctrl+P. Screens that want consistent nvim-style nav use
// this instead of AnsiConsole.Prompt<SelectionPrompt<T>>.
public static class ListPrompt
{
    public static async Task<int?> Show(
        string title,
        IReadOnlyList<string> choices,
        int initialIndex = 0,
        Func<Task>? renderBackdrop = null)
    {
        if (choices.Count == 0) return null;

        var index = Math.Clamp(initialIndex, 0, choices.Count - 1);

        while (true)
        {
            // With a backdrop, the caller's own screen (board/card detail) stays on
            // screen and this panel is appended below it — so the picker doesn't
            // blank out the context the user was just looking at.
            if (renderBackdrop != null)
                await renderBackdrop();
            else
                AnsiConsole.Clear();

            var maxVisible = Math.Max(5, AnsiConsole.Profile.Height / 2);
            var window = ListScrollWindow.Compute(choices.Count, index, maxVisible);

            var table = new Table().Border(TableBorder.Rounded).HideHeaders().Expand();
            table.AddColumn(new TableColumn(""));

            if (window.HasMoreAbove)
                table.AddRow($"[grey]↑ {window.Start} more above[/]");

            for (var i = window.Start; i < window.End; i++)
            {
                var isSelected = i == index;
                var label = Markup.Escape(choices[i]);
                table.AddRow(isSelected ? $"[blue]> {label}[/]" : $"  {label}");
            }

            if (window.HasMoreBelow)
                table.AddRow($"[grey]↓ {choices.Count - window.End} more below[/]");

            var panel = new Panel(table)
            {
                Header = new PanelHeader($" {Markup.Escape(title)} "),
                Border = BoxBorder.Rounded,
            };

            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("[grey]j/k, Ctrl+N/Ctrl+P, or arrows to move; Enter to select; Esc to cancel[/]");

            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.J:
                case ConsoleKey.DownArrow:
                    index = (index + 1) % choices.Count;
                    break;
                case ConsoleKey.N when key.Modifiers == ConsoleModifiers.Control:
                    index = (index + 1) % choices.Count;
                    break;
                case ConsoleKey.K:
                case ConsoleKey.UpArrow:
                    index = (index - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.P when key.Modifiers == ConsoleModifiers.Control:
                    index = (index - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.Enter:
                    return index;
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    public static async Task<int?> ShowMarkup(
        string title,
        IReadOnlyList<string> choices,
        int initialIndex = 0,
        Func<Task>? renderBackdrop = null)
    {
        if (choices.Count == 0) return null;

        var index = Math.Clamp(initialIndex, 0, choices.Count - 1);

        while (true)
        {
            if (renderBackdrop != null)
                await renderBackdrop();
            else
                AnsiConsole.Clear();

            var maxVisible = Math.Max(5, AnsiConsole.Profile.Height / 2);
            var window = ListScrollWindow.Compute(choices.Count, index, maxVisible);

            var table = new Table().Border(TableBorder.Rounded).HideHeaders().Expand();
            table.AddColumn(new TableColumn(""));

            if (window.HasMoreAbove)
                table.AddRow($"[grey]↑ {window.Start} more above[/]");

            for (var i = window.Start; i < window.End; i++)
            {
                var isSelected = i == index;
                // choices[i] may contain Spectre markup — do NOT escape it
                var label = choices[i];
                table.AddRow(isSelected ? $"[blue]> {label}[/]" : $"  {label}");
            }

            if (window.HasMoreBelow)
                table.AddRow($"[grey]↓ {choices.Count - window.End} more below[/]");

            var panel = new Panel(table)
            {
                Header = new PanelHeader($" {Markup.Escape(title)} "),
                Border = BoxBorder.Rounded,
            };

            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("[grey]j/k, Ctrl+N/Ctrl+P, or arrows to move; Enter to select; Esc to cancel[/]");

            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.J:
                case ConsoleKey.DownArrow:
                    index = (index + 1) % choices.Count;
                    break;
                case ConsoleKey.N when key.Modifiers == ConsoleModifiers.Control:
                    index = (index + 1) % choices.Count;
                    break;
                case ConsoleKey.K:
                case ConsoleKey.UpArrow:
                    index = (index - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.P when key.Modifiers == ConsoleModifiers.Control:
                    index = (index - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.Enter:
                    return index;
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }
}
