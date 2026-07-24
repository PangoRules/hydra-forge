using HydraForge.Tui.Models;
using HydraForge.Tui.Services;
using HydraForge.Tui.Generated;

namespace HydraForge.Tui.Services;

public class ApiClientFactory
{
    private readonly ConfigStore _configStore;
    private readonly HttpClient _httpClient;

    public ApiClientFactory(ConfigStore configStore)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        
        _httpClient = new HttpClient(new AuthDelegatingHandler(configStore));
        _httpClient.BaseAddress = new Uri(_configStore.Load().ServerUrl);
    }

    public HydraForgeApiClient CreateClient()
    {
        return new HydraForgeApiClient(_httpClient);
    }

    public HydraForgeApiClient CreateUnauthenticatedClient()
    {
        var unauthenticatedHttpClient = new HttpClient();
        unauthenticatedHttpClient.BaseAddress = new Uri(_configStore.Load().ServerUrl);
        return new HydraForgeApiClient(unauthenticatedHttpClient);
    }
}