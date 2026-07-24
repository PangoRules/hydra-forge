# Plan 4: Auth — Login Screen + Startup Auth Flow

**Branch:** `task/tui-auth`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 4

**Goal:** Login screen with username/password prompts, JWT storage, startup auth flow (config check → token refresh → login fallback).

**Depends on:** Task 2 (ConfigStore), Task 3 (ApiClientFactory).

---

## Step 1: Create `LoginScreen`

Create `src/HydraForge.Tui/Screens/LoginScreen.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LoginScreen(
        ConfigStore configStore,
        ApiClientFactory apiClientFactory,
        AppState appState,
        ErrorCollector errorCollector)
    {
        _configStore = configStore;
        _apiClientFactory = apiClientFactory;
        _appState = appState;
        _errorCollector = errorCollector;
    }

    public Task OnEnterAsync() => Task.CompletedTask;
    public Task OnExitAsync() => Task.CompletedTask;

    public async Task RenderAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("HydraForge").Color(Color.Blue));

        var config = _configStore.Load();

        // Server URL prompt
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

        // Username prompt
        var username = AnsiConsole.Prompt(
            new TextPrompt<string>("Username:")
                .Validate(u => string.IsNullOrWhiteSpace(u)
                    ? ValidationResult.Error("Username required")
                    : ValidationResult.Success()));

        // Password prompt (masked)
        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Password:")
                .Secret()
                .Validate(p => string.IsNullOrWhiteSpace(p)
                    ? ValidationResult.Error("Password required")
                    : ValidationResult.Success()));

        // Attempt login
        await AnsiConsole.Status()
            .StartAsync("Logging in...", async _ =>
            {
                await TryLoginAsync(config, username, password);
            });
    }

    private async Task TryLoginAsync(TuiConfig config, string username, string password)
    {
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/")
            };

            var payload = new { username, password };
            var response = await client.PostAsJsonAsync("api/auth/login", payload, JsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                AnsiConsole.MarkupLine($"[red]Login failed: {response.StatusCode}[/]");
                AnsiConsole.MarkupLine($"[grey]{errorBody}[/]");
                AnsiConsole.WriteLine("Press any key to retry...");
                Console.ReadKey(true);
                await RenderAsync();
                return;
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
            if (loginResponse == null)
            {
                AnsiConsole.MarkupLine("[red]Login failed: invalid response[/]");
                AnsiConsole.WriteLine("Press any key to retry...");
                Console.ReadKey(true);
                await RenderAsync();
                return;
            }

            config.JwtToken = loginResponse.AccessToken;
            config.ExpiresAt = loginResponse.ExpiresAt;
            _configStore.Save(config);

            AnsiConsole.MarkupLine($"[green]Logged in as {loginResponse.Username}[/]");
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]Connection failed: {ex.Message}[/]");
            _errorCollector.Add("N/A", $"Login connection error: {ex.Message}");
            AnsiConsole.WriteLine("Press any key to retry...");
            Console.ReadKey(true);
            await RenderAsync();
        }
    }

    public Task HandleKeyAsync(ConsoleKeyInfo key) => Task.CompletedTask;

    private record LoginResponse(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        Guid UserId,
        string Username,
        bool IsAdmin
    );
}
```

## Step 2: Create startup auth flow in `Program.cs`

Update `src/HydraForge.Tui/Program.cs`:

```csharp
using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;
using HydraForge.Tui.Services;
using Spectre.Console;

namespace HydraForge.Tui;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var appState = new AppState();
        var screenStack = new ScreenStack();
        var configStore = new ConfigStore();
        var errorCollector = new ErrorCollector();
        var apiClientFactory = new ApiClientFactory(configStore, appState, errorCollector);

        // Startup auth flow
        var config = configStore.Load();

        if (string.IsNullOrEmpty(config.JwtToken))
        {
            // No token — show login
            var loginScreen = new LoginScreen(configStore, apiClientFactory, appState, errorCollector);
            await loginScreen.RenderAsync();

            // Reload config after login
            config = configStore.Load();
            if (string.IsNullOrEmpty(config.JwtToken))
            {
                AnsiConsole.MarkupLine("[red]Login failed. Exiting.[/]");
                return 1;
            }
        }
        else if (config.ExpiresAt?.UtcDateTime < DateTime.UtcNow.AddMinutes(1))
        {
            // Token expired or expiring — try refresh
            apiClientFactory.CreateClient();
            var refreshed = await apiClientFactory.RefreshTokenAsync();
            if (!refreshed)
            {
                AnsiConsole.MarkupLine("[yellow]Session expired. Please log in again.[/]");
                configStore.Clear();
                var loginScreen = new LoginScreen(configStore, apiClientFactory, appState, errorCollector);
                await loginScreen.RenderAsync();

                config = configStore.Load();
                if (string.IsNullOrEmpty(config.JwtToken))
                {
                    AnsiConsole.MarkupLine("[red]Login failed. Exiting.[/]");
                    return 1;
                }
            }
        }

        // Initialize API client with valid token
        apiClientFactory.CreateClient();

        AnsiConsole.MarkupLine("[green]Authenticated. Proceeding to project list...[/]");
        AnsiConsole.WriteLine("(Project list screen will be wired in Task 6)");
        AnsiConsole.WriteLine("Press any key to exit (placeholder).");
        Console.ReadKey(true);

        return 0;
    }
}
```

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Screens/LoginScreen.cs src/HydraForge.Tui/Program.cs
git commit -m "feat(tui): add login screen and startup auth flow with token refresh"
```