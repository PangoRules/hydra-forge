using HydraForge.Tui.Models;
using Spectre.Console;

namespace HydraForge.Tui;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var appState = new AppState();
        var screenStack = new ScreenStack();

        AnsiConsole.Write(new FigletText("HydraForge").Color(Color.Blue));
        AnsiConsole.WriteLine("TUI client starting...");

        // Placeholder: will wire up screens in later tasks
        AnsiConsole.WriteLine("Press any key to exit (placeholder).");
        Console.ReadKey(true);

        return 0;
    }
}