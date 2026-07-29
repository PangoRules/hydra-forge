using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;

namespace HydraForge.Tui.Services;

public class ConnectionManager(
    AppState appState,
    ApiClientFactory apiClientFactory,
    ErrorCollector errorCollector
)
{
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var client = apiClientFactory.GetClient();
            // NSwag generates HealthAsync() from the /api/health endpoint
            await client.HealthAsync();
            return true;
        }
        catch (Exception ex)
        {
            errorCollector.Add("N/A", $"Health check failed: {ex.Message}");
            return false;
        }
    }

    public LockScreen CreateLockScreen()
    {
        return new LockScreen(appState, CheckHealthAsync);
    }

    public async Task<bool> WaitForConnectionAsync(int timeoutMs = 30000)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (await CheckHealthAsync())
            {
                appState.Connection = ConnectionStatus.Connected;
                return true;
            }
            appState.Connection = ConnectionStatus.Reconnecting;
            await Task.Delay(2000);
        }
        appState.Connection = ConnectionStatus.Disconnected;
        return false;
    }
}
