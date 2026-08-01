using Spectre.Console;

namespace HydraForge.Tui.Rendering;

// Spectre's AnsiConsole.Profile.Width/Height are captured once at startup and never
// re-queried — every screen sizes tables/panels/columns off them, so a terminal resize
// mid-session (smaller pane, split window) leaves the app rendering at the old, larger
// dimensions and forcing the user to scroll. Console.WindowWidth/Height DO query the
// live terminal on every call, so re-stamping Profile from them before each render
// keeps Spectre's layout math in sync with reality.
public static class ConsoleSize
{
    public static void Sync()
    {
        try
        {
            AnsiConsole.Profile.Width = Console.WindowWidth;
            AnsiConsole.Profile.Height = Console.WindowHeight;
        }
        catch (Exception)
        {
            // Output redirected / no real console attached (headless test runners in
            // particular) — Console.WindowWidth/Height can throw IOException, or return 0,
            // which Spectre's own Profile.Width/Height setters then reject with
            // InvalidOperationException("...must be greater than zero"). Either way, best
            // effort only: keep whatever Profile already has rather than crash the caller.
        }
    }
}
