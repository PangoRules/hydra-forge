using Spectre.Console;

namespace HydraForge.Tui.Rendering;

/// <summary>
/// Prints keybind hints (e.g. "[Enter] Open") flowing left to right, wrapping
/// onto additional lines as the terminal narrows — never splitting a hint
/// across two lines.
/// </summary>
public static class KeyHintBar
{
    private const string Separator = "  ";

    public static void Render(IEnumerable<string> hints)
    {
        foreach (var line in WrapLines(hints, AnsiConsole.Profile.Width))
            AnsiConsole.MarkupLine(line);
    }

    // Split out from Render so full-screen Spectre.Console.Layout consumers (BoardRenderer)
    // can fold the hint bar into a sized Layout region instead of printing it as trailing
    // lines below an already screen-height Layout — extra lines below a full-height Layout
    // push the whole frame up and off the top of the terminal (D-?? / height overflow bug).
    public static List<string> WrapLines(IEnumerable<string> hints, int width)
    {
        width = Math.Max(20, width);

        var lines = new List<string>();
        var line = new List<string>();
        var length = 0;

        foreach (var hint in hints)
        {
            var candidateLength =
                length == 0 ? hint.Length : length + Separator.Length + hint.Length;

            if (line.Count > 0 && candidateLength > width)
            {
                lines.Add(Join(line));
                line = [];
                candidateLength = hint.Length;
            }

            line.Add(hint);
            length = candidateLength;
        }

        if (line.Count > 0)
            lines.Add(Join(line));

        return lines;
    }

    private static string Join(List<string> items) =>
        $"[grey]{string.Join(Separator, items.Select(Markup.Escape))}[/]";
}
