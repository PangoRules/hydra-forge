# Plan 3: ApiClientFactory

**Branch:** `task/tui-apiclient`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 3

**Goal:** Create NSwag-generated HTTP client wrapper with JWT auth header injection and transparent token refresh. No NSwag codegen yet — use raw `HttpClient` + `System.Text.Json` for API calls until NSwag is wired in a follow-up.

**Depends on:** Task 1 (TuiConfig, AppState), Task 2 (ConfigStore).

---

## Step 1: Create `ApiClientFactory`

Create `src/HydraForge.Tui/Services/ApiClientFactory.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public class ApiClientFactory
{
    private readonly ConfigStore _configStore;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private HttpClient? _client;
    private TuiConfig? _cachedConfig;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiClientFactory(ConfigStore configStore, AppState appState, ErrorCollector errorCollector)
    {
        _configStore = configStore;
        _appState = appState;
        _errorCollector = errorCollector;
    }

    public HttpClient CreateClient()
    {
        var config = _configStore.Load();
        _cachedConfig = config;

        var handler = new HttpClientHandler();
        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/")
        };

        if (!string.IsNullOrEmpty(config.JwtToken))
        {
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.JwtToken);
        }

        return _client;
    }

    public HttpClient GetClient()
    {
        if (_client == null)
            return CreateClient();
        return _client;
    }

    public async Task<bool> RefreshTokenAsync()
    {
        var config = _cachedConfig ?? _configStore.Load();
        if (string.IsNullOrEmpty(config.JwtToken))
            return false;

        try
        {
            using var refreshClient = new HttpClient
            {
                BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/")
            };
            refreshClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.JwtToken);

            var response = await refreshClient.PostAsync("api/auth/refresh", null);
            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(JsonOptions);
            if (body == null)
                return false;

            config.JwtToken = body.AccessToken;
            config.ExpiresAt = body.ExpiresAt;
            _configStore.Save(config);
            _cachedConfig = config;

            // Update existing client's auth header
            if (_client != null)
            {
                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", config.JwtToken);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsTokenExpiringSoon()
    {
        var config = _cachedConfig ?? _configStore.Load();
        if (config.ExpiresAt == null)
            return false;
        return config.ExpiresAt.Value.UtcDateTime - DateTime.UtcNow < TimeSpan.FromSeconds(60);
    }

    private record RefreshTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
```

## Step 2: Create `ErrorCollector` stub

Create `src/HydraForge.Tui/Services/ErrorCollector.cs` (stub — full impl in Task 5):

```csharp
namespace HydraForge.Tui.Services;

public class ErrorCollector
{
    private readonly List<(DateTime Timestamp, string CorrelationId, string Message)> _errors = new();

    public void Add(string correlationId, string message)
    {
        _errors.Add((DateTime.UtcNow, correlationId, message));
        if (_errors.Count > 50)
            _errors.RemoveAt(0);
    }

    public IReadOnlyList<(DateTime Timestamp, string CorrelationId, string Message)> GetErrors()
        => _errors.AsReadOnly();

    public void Dismiss(int index)
    {
        if (index >= 0 && index < _errors.Count)
            _errors.RemoveAt(index);
    }

    public int Count => _errors.Count;
}
```

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Services/ApiClientFactory.cs src/HydraForge.Tui/Services/ErrorCollector.cs
git commit -m "feat(tui): add ApiClientFactory with JWT auth and token refresh"
```