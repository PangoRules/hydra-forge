using System.Net;
using HydraForge.Tui.Services;

namespace HydraForge.Tui.Tests;

public class AuthDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_WithToken_AddsBearerAuthorizationHeader()
    {
        var fake = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new AuthDelegatingHandler();
        handler.SetToken("jwt-abc");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        await client.GetAsync("ping");

        Assert.Equal("Bearer", fake.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("jwt-abc", fake.LastRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SendAsync_AfterSetTokenNull_OmitsAuthorizationHeader()
    {
        var fake = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new AuthDelegatingHandler();
        handler.SetToken("jwt-abc");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        handler.SetToken(null);
        await client.GetAsync("ping");

        Assert.Null(fake.LastRequest!.Headers.Authorization);
    }
}
