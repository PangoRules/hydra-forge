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

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        // No-op for login screen
    }

    public async Task OnEnterAsync()
    {
        // No-op - login flow moved to RenderAsync
    }

    public async Task RenderAsync()
    {
        // Load existing config (if any)
        var config = _configStore.Load();

        // Prompt for server URL
        var serverUrl = AnsiConsole.Prompt(
            new TextPrompt<string>("Server URL:")
                .DefaultValue(config.ServerUrl)
                .Validate(url =>
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out _))
                        return ValidationResult.Success();
                    return ValidationResult.Error("Enter a valid URL (e.g. http://localhost:5000)");
                }));
        config.ServerUrl = serverUrl;

        // Prompt for username and password in a retry loop
        while (true)
        {
            // Prompt for username and password
            var username = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Username:[/]")
                    .ValidationErrorMessage("[red]Username is required[/]")
                    .Validate(u => !string.IsNullOrWhiteSpace(u) 
                        ? ValidationResult.Success() 
                        : ValidationResult.Error("Username is required")));

            var password = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Password:[/]")
                    .Secret()
                    .ValidationErrorMessage("[red]Password is required[/]")
                    .Validate(u => !string.IsNullOrWhiteSpace(u) 
                        ? ValidationResult.Success() 
                        : ValidationResult.Error("Password is required")));

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

                AnsiConsole.MarkupLine("[green]Login successful![/]");
                AnsiConsole.MarkupLine("Press any key to continue...");
                Console.ReadKey(true);
                break; // Successful login, exit the loop
            }
            catch (ApiException ex)
            {
                // Log the error and show it to the user
                var error = $"Login failed: {ex.Message}";
                _errorCollector.Add("N/A", $"Login error: {ex.Message}");
                AnsiConsole.MarkupLine($"[red]{error}[/]");
                AnsiConsole.MarkupLine("Press any key to try again...");
                Console.ReadKey(true);
            }
            catch (HttpRequestException ex)
            {
                // Network error
                var error = $"Network error during login: {ex.Message}";
                _errorCollector.Add("N/A", $"Login error: {ex.Message}");
                AnsiConsole.MarkupLine($"[red]{error}[/]");
                AnsiConsole.MarkupLine("Press any key to try again...");
                Console.ReadKey(true);
            }
        }
    }

    public async Task OnExitAsync()
    {
        // No cleanup needed
    }
}