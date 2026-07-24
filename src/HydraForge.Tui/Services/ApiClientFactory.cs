using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public class ApiClientFactory
{
    private readonly ConfigStore _configStore;
    private readonly AppState _appState;
    private readonly ErrorCollector _errorCollector;

    private readonly Func<HttpMessageHandler> _innerHandlerFactory;

    private HydraForgeApiClient? _client;
    private AuthDelegatingHandler? _authHandler;
    private TuiConfig? _cachedConfig;

    public ApiClientFactory(ConfigStore configStore, AppState appState, ErrorCollector errorCollector)
        : this(configStore, appState, errorCollector, () => new HttpClientHandler())
    {
    }

    // Lets tests substitute a fake transport instead of hitting the real network.
    internal ApiClientFactory(
        ConfigStore configStore,
        AppState appState,
        ErrorCollector errorCollector,
        Func<HttpMessageHandler> innerHandlerFactory)
    {
        _configStore = configStore;
        _appState = appState;
        _errorCollector = errorCollector;
        _innerHandlerFactory = innerHandlerFactory;
    }

    /// <summary>
    /// Creates or returns the cached authenticated NSwag client.
    /// Reads JWT from config and injects it via AuthDelegatingHandler.
    /// </summary>
    public HydraForgeApiClient CreateClient()
    {
        var config = _configStore.Load();
        _cachedConfig = config;

        _authHandler = new AuthDelegatingHandler(_innerHandlerFactory());
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
        var httpClient = new HttpClient(_innerHandlerFactory())
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