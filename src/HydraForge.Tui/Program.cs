using HydraForge.Tui.Models;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace HydraForge.Tui;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var config = new TuiConfig();
        configuration.Bind(config);

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