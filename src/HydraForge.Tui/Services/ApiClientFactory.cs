using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public class ApiClientFactory
{
    private readonly ConfigStore _configStore;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;
    private readonly HttpMessageHandler? _testHandler;

    private HydraForgeApiClient? _client;
    private AuthDelegatingHandler? _authHandler;
    private TuiConfig? _cachedConfig;
    private DateTime _lastConfigLoad = DateTime.MinValue;
    private HttpClient? _rawHttpClient;

    public ApiClientFactory(ConfigStore configStore, AppState appState, ErrorCollector errorCollector, HttpMessageHandler? testHandler = null)
    {
        _configStore = configStore;
        _appState = appState;
        _errorCollector = errorCollector;
        _testHandler = testHandler;
    }

    /// <summary>
    /// Invalidates the cached config to force a reload on next access.
    /// </summary>
    public void InvalidateConfig()
    {
        _cachedConfig = null;
        _lastConfigLoad = DateTime.MinValue;
    }

    /// <summary>
    /// Creates or returns the cached authenticated NSwag client.
    /// Reads JWT from config and injects it via AuthDelegatingHandler.
    /// </summary>
    public virtual HydraForgeApiClient CreateClient()
    {
        var config = _configStore.Load();
        _cachedConfig = config;

        _authHandler = _testHandler != null 
            ? new AuthDelegatingHandler(_testHandler) 
            : new AuthDelegatingHandler();
        _authHandler.SetToken(config.JwtToken);

        var httpClient = new HttpClient(_authHandler)
        {
            BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/")
        };

        _client = new HydraForgeApiClient(httpClient);
        return _client;
    }

    /// <summary>
    /// Creates an authenticated raw HttpClient for endpoints NSwag codegen
    /// couldn't type-check (e.g. checklist/comments/relationships GETs).
    /// </summary>
    public HttpClient CreateAuthenticatedHttpClient()
    {
        if (_rawHttpClient != null)
            return _rawHttpClient;
            
        var config = _configStore.Load();
        _cachedConfig = config;

        var authHandler = _testHandler != null
            ? new AuthDelegatingHandler(_testHandler)
            : new AuthDelegatingHandler();
        authHandler.SetToken(config.JwtToken);

        _rawHttpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/")
        };
        
        return _rawHttpClient;
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
    public virtual HydraForgeApiClient CreateUnauthenticatedClient()
    {
        var config = _cachedConfig ?? _configStore.Load();
        var httpClient = _testHandler != null
            ? new HttpClient(_testHandler)
            : new HttpClient();
        httpClient.BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/");
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
