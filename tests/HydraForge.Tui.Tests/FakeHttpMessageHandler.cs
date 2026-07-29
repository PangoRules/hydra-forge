namespace HydraForge.Tui.Tests;

// Stands in for the real network transport in ApiClientFactory/AuthDelegatingHandler
// tests, via the internal inner-handler seam those classes expose for testing.
internal sealed class FakeHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder
) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        LastRequest = request;
        return Task.FromResult(responder(request));
    }
}
