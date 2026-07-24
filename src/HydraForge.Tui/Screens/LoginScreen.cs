using System.Net.Http.Json;
using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class LoginScreen : IScreen
{
    private readonly ConfigStore _configStore;
    private readonly ApiClientFactory _apiClientFactory;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;

    public LoginScreen(ConfigStore configStore, ApiClientFactory apiClientFactory, AppState appState, ErrorCollector errorCollector)
    {
        _configStore = configStore;
        _apiClientFactory = apiClientFactory;
        _appState = appState;
        _errorCollector = errorCollector;
    }

    public async Task RenderAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("HydraForge").Color(Color.Blue));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Please log in to continue:");
        AnsiConsole.WriteLine();
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        // No-op for login screen
    }

    public async Task OnEnterAsync()
    {
        // Load existing config (if any)
        var config = _configStore.Load();

        // Prompt for username and password
        var username = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Username:[/]")
                .ValidationErrorMessage("[red]Username is required[/]")
                .Validate(username => username.Length > 0 ? ValidationResult.Success() : ValidationResult.Error("Username is required")));

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Password:[/]")
                .Secret()
                .ValidationErrorMessage("[red]Password is required[/]")
                .Validate(password => password.Length > 0 ? ValidationResult.Success() : ValidationResult.Error("Password is required")));

        // Attempt login
        try
        {
            var client = _apiClientFactory.CreateUnauthenticatedClient();
            var loginRequest = new LoginRequest
            {
                Username = username,
                Password = password
            };

            var response = await client.LoginAsync(loginRequest);

            // Save the JWT token and other details to config
            config.JwtToken = response.AccessToken;
            config.ExpiresAt = response.ExpiresAt;
            // Note: RefreshToken is not part of LoginResponse, it's handled by the refresh endpoint
            _configStore.Save(config);

            // Update app state
            _appState.CurrentScreen = null; // Will be set by the main loop after auth

            AnsiConsole.WriteLine("[green]Login successful![/]");
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
        catch (ApiException ex)
        {
            // Log the error and show it to the user
            var error = $"Login failed: {ex.Message}";
            _errorCollector.Add(error, "LoginScreen");
            AnsiConsole.WriteLine($"[red]{error}[/]");
            AnsiConsole.WriteLine("Press any key to try again...");
            Console.ReadKey(true);
        }
        catch (HttpRequestException ex)
        {
            // Network error
            var error = $"Network error during login: {ex.Message}";
            _errorCollector.Add(error, "LoginScreen");
            AnsiConsole.WriteLine($"[red]{error}[/]");
            AnsiConsole.WriteLine("Press any key to try again...");
            Console.ReadKey(true);
        }
    }

    public async Task OnExitAsync()
    {
        // No cleanup needed
    }
}