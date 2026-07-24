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

    // Lets tests substitute a fake transport instead of hitting the real network.
    internal AuthDelegatingHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

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