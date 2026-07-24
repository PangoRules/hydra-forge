using Microsoft.Extensions.Configuration;
using Spectre.Console;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using HydraForge.Tui.Screens;

namespace HydraForge.Tui;

public static class Program
{
    public static async Task<int> Main()
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

        AnsiConsole.Write(new FigletText("HydraForge").Color(Color.Blue));
        AnsiConsole.WriteLine("TUI client starting...");

        // Initialize services
        var configStore = new ConfigStore();
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var apiClientFactory = new ApiClientFactory(configStore, appState, errorCollector);

        // Check if we have a valid JWT token
        var loadedConfig = configStore.Load();
        var needsLogin = string.IsNullOrEmpty(loadedConfig.JwtToken);

        if (needsLogin)
        {
            // Show login screen
            var loginScreen = new LoginScreen(configStore, apiClientFactory, appState, errorCollector);
            await loginScreen.OnEnterAsync();
        }
        else
        {
            // Check if token is expiring soon
            if (apiClientFactory.IsTokenExpiringSoon())
            {
                // Try to refresh the token
                var refreshSuccess = await apiClientFactory.RefreshTokenAsync();
                if (!refreshSuccess)
                {
                    // Refresh failed, clear config and show login screen
                    configStore.Clear();
                    var loginScreen = new LoginScreen(configStore, apiClientFactory, appState, errorCollector);
                    await loginScreen.OnEnterAsync();
                }
            }
        }

        // Create authenticated client for subsequent API calls
        var client = apiClientFactory.CreateClient();

        // Placeholder: will wire up screens in later tasks
        AnsiConsole.WriteLine("Authentication successful. Press any key to continue...");
        Console.ReadKey(true);

        return 0;
    }
}