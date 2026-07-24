using System.Net.Http.Headers;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly ConfigStore _configStore;

    public AuthDelegatingHandler(ConfigStore configStore)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var config = _configStore.Load();
        if (!string.IsNullOrEmpty(config.JwtToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.JwtToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}