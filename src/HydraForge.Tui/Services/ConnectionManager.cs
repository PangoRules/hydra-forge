using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;

namespace HydraForge.Tui.Services;

public class ConnectionManager
{
    private readonly AppState _appState;
    private readonly ApiClientFactory _apiClientFactory;
    private readonly ErrorCollector _errorCollector;

    public ConnectionManager(
        AppState appState,
        ApiClientFactory apiClientFactory,
        ErrorCollector errorCollector)
    {
        _appState = appState;
        _apiClientFactory = apiClientFactory;
        _errorCollector = errorCollector;
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var client = _apiClientFactory.GetClient();
            // NSwag generates HealthAsync() from the /api/health endpoint
            await client.HealthAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public LockScreen CreateLockScreen()
    {
        return new LockScreen(_appState, CheckHealthAsync);
    }

    public async Task<bool> WaitForConnectionAsync(int timeoutMs = 30000)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (await CheckHealthAsync())
            {
                _appState.Connection = ConnectionStatus.Connected;
                return true;
            }
            _appState.Connection = ConnectionStatus.Reconnecting;
            await Task.Delay(2000);
        }
        _appState.Connection = ConnectionStatus.Disconnected;
        return false;
    }
}
