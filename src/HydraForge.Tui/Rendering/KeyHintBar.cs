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
        var width = Math.Max(20, AnsiConsole.Profile.Width);

        var line = new List<string>();
        var length = 0;

        foreach (var hint in hints)
        {
            var candidateLength = length == 0 ? hint.Length : length + Separator.Length + hint.Length;

            if (line.Count > 0 && candidateLength > width)
            {
                Flush(line);
                line = new List<string>();
                candidateLength = hint.Length;
            }

            line.Add(hint);
            length = candidateLength;
        }

        if (line.Count > 0)
            Flush(line);
    }

    private static void Flush(List<string> items) =>
        AnsiConsole.MarkupLine($"[grey]{string.Join(Separator, items.Select(Markup.Escape))}[/]");
}
