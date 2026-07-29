using System.Net;
using System.Text;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;

namespace HydraForge.Tui.Tests;

public class ApiClientFactoryTests : IDisposable
{
    private readonly string _configDir;
    private readonly ConfigStore _configStore;

    public ApiClientFactoryTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "hydraforge-apiclientfactory-tests-" + Guid.NewGuid());
        _configStore = new ConfigStore(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    private static HttpResponseMessage RefreshSuccessResponse(string token, DateTimeOffset expiresAt)
    {
        var json = $"{{\"accessToken\":\"{token}\",\"expiresAt\":\"{expiresAt:O}\"}}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public async Task CreateClient_UsesServerUrlFromConfigAsBaseAddress()
    {
        _configStore.Save(new TuiConfig { ServerUrl = "https://example.test/", JwtToken = "jwt-1" });
        var fake = new FakeHttpMessageHandler(_ => RefreshSuccessResponse("t", DateTimeOffset.UtcNow.AddHours(1)));
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);

        var client = factory.CreateClient();
        await client.RefreshAsync();

        Assert.StartsWith("https://example.test/", fake.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateClient_InjectsAuthorizationHeaderWithConfiguredJwt()
    {
        _configStore.Save(new TuiConfig { ServerUrl = "https://example.test/", JwtToken = "jwt-1" });
        var fake = new FakeHttpMessageHandler(_ => RefreshSuccessResponse("t", DateTimeOffset.UtcNow.AddHours(1)));
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);

        var client = factory.CreateClient();
        await client.RefreshAsync();

        Assert.Equal("Bearer", fake.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("jwt-1", fake.LastRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public void GetClient_CalledTwice_ReturnsSameCachedInstance()
    {
        _configStore.Save(new TuiConfig { ServerUrl = "https://example.test/", JwtToken = "jwt-1" });
        var fake = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);

        var first = factory.GetClient();
        var second = factory.GetClient();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task CreateUnauthenticatedClient_OmitsAuthorizationHeader()
    {
        _configStore.Save(new TuiConfig { ServerUrl = "https://example.test/", JwtToken = "jwt-1" });
        var fake = new FakeHttpMessageHandler(_ => RefreshSuccessResponse("t", DateTimeOffset.UtcNow.AddHours(1)));
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);

        var client = factory.CreateUnauthenticatedClient();
        await client.RefreshAsync();

        Assert.Null(fake.LastRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task RefreshTokenAsync_OnSuccess_PersistsNewTokenAndUsesItOnSubsequentRequests()
    {
        _configStore.Save(new TuiConfig
        {
            ServerUrl = "https://example.test/",
            JwtToken = "old-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1)
        });
        var newExpiry = DateTimeOffset.UtcNow.AddHours(1);
        var fake = new FakeHttpMessageHandler(_ => RefreshSuccessResponse("new-token", newExpiry));
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);
        var client = factory.CreateClient();

        var result = await factory.RefreshTokenAsync();

        Assert.True(result);
        var persisted = _configStore.Load();
        Assert.Equal("new-token", persisted.JwtToken);
        Assert.Equal(newExpiry, persisted.ExpiresAt);

        // Happy 7: a subsequent authenticated call must carry the refreshed token.
        await client.RefreshAsync();
        Assert.Equal("new-token", fake.LastRequest!.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithEmptyJwtToken_ReturnsFalseWithoutCallingApi()
    {
        _configStore.Save(new TuiConfig { ServerUrl = "https://example.test/", JwtToken = "" });
        var fake = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("API must not be called"));
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);

        var result = await factory.RefreshTokenAsync();

        Assert.False(result);
        Assert.Null(fake.LastRequest);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenServerReturnsUnauthorized_ReturnsFalseAndKeepsExistingToken()
    {
        _configStore.Save(new TuiConfig { ServerUrl = "https://example.test/", JwtToken = "old-token" });
        var fake = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);

        var result = await factory.RefreshTokenAsync();

        Assert.False(result);
        Assert.Equal("old-token", _configStore.Load().JwtToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenServerReturns5xx_ReturnsFalseAndKeepsExistingToken()
    {
        _configStore.Save(new TuiConfig { ServerUrl = "https://example.test/", JwtToken = "old-token" });
        var fake = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);

        var result = await factory.RefreshTokenAsync();

        Assert.False(result);
        Assert.Equal("old-token", _configStore.Load().JwtToken);
    }

    [Fact]
    public void IsTokenExpiringSoon_WhenExpiryMoreThan60SecondsAway_ReturnsFalse()
    {
        _configStore.Save(new TuiConfig { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), null);

        Assert.False(factory.IsTokenExpiringSoon());
    }

    [Fact]
    public void IsTokenExpiringSoon_WhenExpiryWithin60Seconds_ReturnsTrue()
    {
        _configStore.Save(new TuiConfig { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30) });
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), null);

        Assert.True(factory.IsTokenExpiringSoon());
    }

    [Fact]
    public void IsTokenExpiringSoon_WhenExpiryIsNull_ReturnsFalse()
    {
        _configStore.Save(new TuiConfig { ExpiresAt = null });
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), null);

        Assert.False(factory.IsTokenExpiringSoon());
    }
}
