using HydraForge.Server.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HydraForge.Server.Tests;

public class CorrelationIdMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorrelationIdMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_without_XCorrelationId_returns_header_with_generated_value()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/weatherforecast");

        response.EnsureSuccessStatusCode();
        var correlationId = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();

        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
        Assert.StartsWith("req_", correlationId);
        Assert.True(correlationId.Length <= 128);
    }

    [Fact]
    public async Task Request_with_XCorrelationId_returns_same_header()
    {
        var client = _factory.CreateClient();
        var testCorrelationId = "test-corr-12345";

        var request = new HttpRequestMessage(HttpMethod.Get, "/weatherforecast");
        request.Headers.Add("X-Correlation-Id", testCorrelationId);

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var returnedCorrelationId = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();

        Assert.Equal(testCorrelationId, returnedCorrelationId);
    }

    [Fact]
    public async Task Exception_ProblemDetails_includes_correlation_id()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/throw");
        request.Headers.Add("X-Correlation-Id", "test-exception-corr");

        var response = await client.SendAsync(request);

        Assert.Equal(500, (int)response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("test-exception-corr", content);
    }

    [Fact]
    public async Task CorrelationId_rejects_values_over_128_chars()
    {
        var client = _factory.CreateClient();
        var longCorrelationId = new string('a', 129);

        var request = new HttpRequestMessage(HttpMethod.Get, "/weatherforecast");
        request.Headers.Add("X-Correlation-Id", longCorrelationId);

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var returnedCorrelationId = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();

        Assert.NotEqual(longCorrelationId, returnedCorrelationId);
        Assert.StartsWith("req_", returnedCorrelationId);
    }
}