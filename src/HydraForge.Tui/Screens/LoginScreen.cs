using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class LoginScreen(
    ConfigStore configStore,
    ApiClientFactory apiClientFactory,
    AppState appState,
    ErrorCollector errorCollector
) : IScreen
{
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
        var config = configStore.Load();

        // Prompt for server URL
        var serverUrl = AnsiConsole.Prompt(
            new TextPrompt<string>("Server URL:")
                .DefaultValue(config.ServerUrl)
                .Validate(url =>
                {
                    if (
                        Uri.TryCreate(url, UriKind.Absolute, out var uri)
                        && (uri.Scheme == "http" || uri.Scheme == "https")
                        && !string.IsNullOrWhiteSpace(uri.Host)
                    )
                        return ValidationResult.Success();
                    return ValidationResult.Error(
                        "Enter a valid HTTP or HTTPS URL (e.g. http://localhost:5000)"
                    );
                })
        );
        config.ServerUrl = serverUrl;
        configStore.Save(config);
        apiClientFactory.InvalidateConfig();

        // Prompt for username and password in a retry loop
        while (true)
        {
            // Prompt for username and password
            var username = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Username:[/]")
                    .ValidationErrorMessage("[red]Username is required[/]")
                    .Validate(u =>
                        !string.IsNullOrWhiteSpace(u)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("Username is required")
                    )
            );

            var password = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Password:[/]")
                    .Secret()
                    .ValidationErrorMessage("[red]Password is required[/]")
                    .Validate(u =>
                        !string.IsNullOrWhiteSpace(u)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("Password is required")
                    )
            );

            // Attempt login
            try
            {
                var client = apiClientFactory.CreateUnauthenticatedClient();
                var loginRequest = new LoginRequest { Username = username, Password = password };

                var response = await client.LoginAsync(loginRequest);

                // Save the JWT token and other details to config
                config.JwtToken = response.AccessToken;
                config.ExpiresAt = response.ExpiresAt;
                // Note: RefreshToken is not part of LoginResponse, it's handled by the refresh endpoint
                configStore.Save(config);

                // Verify that we got a valid token
                if (string.IsNullOrWhiteSpace(response.AccessToken))
                {
                    var error = "Login failed: No access token received";
                    errorCollector.Add("N/A", $"Login error: {error}");
                    AnsiConsole.MarkupLine($"[red]{error}[/]");
                    AnsiConsole.MarkupLine("Press any key to try again...");
                    Console.ReadKey(true);
                    continue; // Retry login
                }

                // Update app state
                appState.CurrentScreen = null; // Will be set by the main loop after auth

                AnsiConsole.MarkupLine("[green]Login successful![/]");
                AnsiConsole.MarkupLine("Press any key to continue...");
                Console.ReadKey(true);
                break; // Successful login, exit the loop
            }
            catch (ApiException ex)
            {
                // Log the error and show it to the user
                var error = $"Login failed: {ex.Message}";
                errorCollector.Add("N/A", $"Login error: {ex.Message}");
                var escapedError = Markup.Escape(error);
                AnsiConsole.MarkupLine($"[red]{escapedError}[/]");
                AnsiConsole.MarkupLine("Press any key to try again...");
                Console.ReadKey(true);
            }
            catch (HttpRequestException ex)
            {
                // Network error
                var error = $"Network error during login: {ex.Message}";
                errorCollector.Add("N/A", $"Login error: {ex.Message}");
                var escapedError = Markup.Escape(error);
                AnsiConsole.MarkupLine($"[red]{escapedError}[/]");
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
