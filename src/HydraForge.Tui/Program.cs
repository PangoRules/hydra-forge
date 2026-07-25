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
        var connectionManager = new ConnectionManager(appState, apiClientFactory, errorCollector);

        // Gate startup on server reachability — show the lock screen and
        // auto-retry until the server answers, instead of letting the user
        // type credentials at a dead server.
        if (!await connectionManager.CheckHealthAsync())
        {
            await RunLockScreenUntilConnectedAsync(connectionManager.CreateLockScreen(), appState);
        }

        // Check if we have a valid JWT token
        var loadedConfig = configStore.Load();
        var needsLogin = string.IsNullOrWhiteSpace(loadedConfig.JwtToken) || loadedConfig.ExpiresAt == null;

        if (needsLogin)
        {
            // Show login screen
            var loginScreen = new LoginScreen(configStore, apiClientFactory, appState, errorCollector);
            await loginScreen.RenderAsync();
            
            // Reload config after login
            loadedConfig = configStore.Load();
            
            // Check if login was successful
            if (string.IsNullOrWhiteSpace(loadedConfig.JwtToken) || loadedConfig.ExpiresAt == null)
            {
                AnsiConsole.MarkupLine("[red]Login failed. Exiting.[/]");
                return 1;
            }
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
                    await loginScreen.RenderAsync();
                    
                    // Reload config after login
                    loadedConfig = configStore.Load();
                    
                    // Check if login was successful
                    if (string.IsNullOrWhiteSpace(loadedConfig.JwtToken) || loadedConfig.ExpiresAt == null)
                    {
                        AnsiConsole.MarkupLine("[red]Login failed. Exiting.[/]");
                        return 1;
                    }
                }
            }
        }

        // Initialize API client with valid token
        apiClientFactory.CreateClient();

        // Show project list
        var projectListScreen = new ProjectListScreen(
            apiClientFactory, appState, errorCollector, connectionManager);
        appState.CurrentScreen = projectListScreen;
        await projectListScreen.OnEnterAsync();
        await projectListScreen.RenderAsync();

        // Main input loop
        while (true)
        {
            var key = Console.ReadKey(true);

            // Global shortcuts
            if (key.Key == ConsoleKey.Q && (key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                Environment.Exit(0);
            }

            if (appState.CurrentScreen != null)
            {
                await appState.CurrentScreen.HandleKeyAsync(key);
            }
        }

#pragma warning disable CS0162
        return 0;
#pragma warning restore CS0162
    }

    // No generic IScreen runner exists yet (board loop lands in a later task),
    // so LockScreen drives its own render/input loop here until its background
    // retry loop marks AppState.Connection as Connected.
    private static async Task RunLockScreenUntilConnectedAsync(LockScreen lockScreen, AppState appState)
    {
        await lockScreen.OnEnterAsync();

        while (appState.Connection != ConnectionStatus.Connected)
        {
            await lockScreen.RenderAsync();

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                await lockScreen.HandleKeyAsync(key);
            }
            else
            {
                await Task.Delay(150);
            }
        }

        await lockScreen.OnExitAsync();
    }
}