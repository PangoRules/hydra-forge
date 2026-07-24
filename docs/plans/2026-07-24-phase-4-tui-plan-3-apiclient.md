# Plan 3: ApiClientFactory

**Branch:** `task/tui-apiclient`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 3

**Goal:** Wrap NSwag-generated `HydraForgeApiClient` with JWT auth header injection via custom `DelegatingHandler` and transparent token refresh. No raw `HttpClient` — all API calls go through the typed NSwag client.

**Depends on:** Task 1 (NSwag codegen produces `HydraForgeApiClient` in `Generated/`), Task 2 (ConfigStore).

---

## Step 1: Create `AuthDelegatingHandler`

Create `src/HydraForge.Tui/Services/AuthDelegatingHandler.cs`:

```csharp
using System.Net.Http.Headers;

namespace HydraForge.Tui.Services;

/// <summary>
/// DelegatingHandler that injects the JWT Bearer token into every outgoing request.
/// Token is mutable — ApiClientFactory updates it on refresh.
/// </summary>
public class AuthDelegatingHandler : DelegatingHandler
{
    private string? _token;

    public AuthDelegatingHandler() : base(new HttpClientHandler()) { }

    public void SetToken(string? token) => _token = token;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

## Step 2: Create `ApiClientFactory`

Create `src/HydraForge.Tui/Services/ApiClientFactory.cs`:

```csharp
using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public class ApiClientFactory
{
    private readonly ConfigStore _configStore;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;

    private HydraForgeApiClient? _client;
    private AuthDelegatingHandler? _authHandler;
    private TuiConfig? _cachedConfig;

    public ApiClientFactory(ConfigStore configStore, AppState appState, ErrorCollector errorCollector)
    {
        _configStore = configStore;
        _appState = appState;
        _errorCollector = errorCollector;
    }

    /// <summary>
    /// Creates or returns the cached authenticated NSwag client.
    /// Reads JWT from config and injects it via AuthDelegatingHandler.
    /// </summary>
    public HydraForgeApiClient CreateClient()
    {
        var config = _configStore.Load();
        _cachedConfig = config;

        _authHandler = new AuthDelegatingHandler();
        _authHandler.SetToken(config.JwtToken);

        var httpClient = new HttpClient(_authHandler)
        {
            BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/")
        };

        _client = new HydraForgeApiClient(httpClient);
        return _client;
    }

    /// <summary>
    /// Returns the cached client, or creates a new one if none exists.
    /// </summary>
    public HydraForgeApiClient GetClient()
    {
        if (_client == null)
            return CreateClient();
        return _client;
    }

    /// <summary>
    /// Creates an unauthenticated NSwag client (no JWT header).
    /// Used only for the login endpoint.
    /// </summary>
    public HydraForgeApiClient CreateUnauthenticatedClient()
    {
        var config = _cachedConfig ?? _configStore.Load();
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/")
        };
        return new HydraForgeApiClient(httpClient);
    }

    /// <summary>
    /// Refreshes the JWT token via the auth/refresh endpoint.
    /// Updates config, cached client's auth handler, and returns success.
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        var config = _cachedConfig ?? _configStore.Load();
        if (string.IsNullOrEmpty(config.JwtToken))
            return false;

        try
        {
            // Use unauthenticated client for refresh — the refresh endpoint
            // reads the token from the Authorization header, which our
            // AuthDelegatingHandler already injects on the authenticated client.
            // But since we're refreshing because the token might be expired,
            // we use the existing client (which has the old token in its handler).
            var client = GetClient();

            // NSwag generates RefreshAsync based on the OpenAPI spec.
            // If the spec doesn't expose a typed refresh method, fall back to
            // a raw POST on the base HttpClient.
            var refreshResponse = await client.RefreshAsync();

            config.JwtToken = refreshResponse.AccessToken;
            config.ExpiresAt = refreshResponse.ExpiresAt;
            _configStore.Save(config);
            _cachedConfig = config;

            // Update the auth handler so subsequent requests use the new token
            _authHandler?.SetToken(config.JwtToken);

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
}
```

**Design notes:**
- `HydraForgeApiClient` is the NSwag-generated class from Task 1. It takes an `HttpClient` in its constructor (`injectHttpClient: true` in nswag.json).
- `AuthDelegatingHandler` is a standard `DelegatingHandler` — it sits in the `HttpClient` pipeline and adds the `Authorization` header to every request. No manual header setting in call sites.
- `CreateUnauthenticatedClient()` returns a client without the auth handler — used only by `LoginScreen` for the login POST.
- Token refresh calls `client.RefreshAsync()` — the NSwag-generated method. If the OpenAPI spec doesn't expose a typed refresh endpoint, the implementation falls back to a raw POST. The exact method name depends on the operation ID in the OpenAPI spec.
- All API calls in later tasks use `_apiClientFactory.GetClient()` which returns `HydraForgeApiClient`. Call sites call typed methods like `client.ProjectsAsync()` instead of `client.GetAsync("api/projects")`.

## Step 3: Create `ErrorCollector` stub

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

## Step 4: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds. `HydraForgeApiClient` from Task 1's NSwag codegen must exist in `Generated/`.

## Step 5: Commit

```bash
git add src/HydraForge.Tui/Services/AuthDelegatingHandler.cs src/HydraForge.Tui/Services/ApiClientFactory.cs src/HydraForge.Tui/Services/ErrorCollector.cs
git commit -m "feat(tui): add ApiClientFactory wrapping NSwag client with JWT auth handler"
```