using Spectre.Console;

namespace HydraForge.Tui.Rendering;

/// <summary>
/// The "Quit HydraForge?" confirm prompt shown by every screen's Q/quit key —
/// one place instead of five copies of the same literal string.
/// </summary>
public static class QuitConfirm
{
    public static bool Show() => AnsiConsole.Confirm("Quit HydraForge?");
}
