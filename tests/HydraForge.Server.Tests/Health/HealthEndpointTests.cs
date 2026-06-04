using System.Net;
using HydraForge.Server.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HydraForge.Server.Tests.Health;

public class HealthEndpointTests
{
    [Fact]
    public async Task GetHealth_ReturnsComponents()
    {
        var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("server", body);
        Assert.Contains("database", body);
        Assert.Contains("llmProviders", body);
    }

    [Fact]
    public async Task GetHealth_ReturnsCorrelationHeader()
    {
        var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }
}

class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            // Remove existing IHealthProbe registrations (singleton GetHealthHandler already constructed with them)
            var removeDescriptors = services.Where(d =>
                d.ServiceType == typeof(HydraForge.Application.Health.IHealthProbe)).ToList();
            foreach (var d in removeDescriptors)
                services.Remove(d);

            // Add test fakes
            services.AddScoped<HydraForge.Application.Health.IHealthProbe, FakeServerHealthProbe>();
            services.AddScoped<HydraForge.Application.Health.IHealthProbe, FakeDbHealthProbe>();
            services.AddScoped<HydraForge.Application.Health.IHealthProbe, FakeLlmHealthProbe>();
        });
    }
}

internal class FakeServerHealthProbe : HydraForge.Application.Health.IHealthProbe
{
    public Task<HydraForge.Application.Health.HealthStatus> CheckAsync(CancellationToken ct = default)
        => Task.FromResult(HydraForge.Application.Health.HealthStatus.Healthy);
}

internal class FakeDbHealthProbe : HydraForge.Application.Health.IHealthProbe
{
    public Task<HydraForge.Application.Health.HealthStatus> CheckAsync(CancellationToken ct = default)
        => Task.FromResult(HydraForge.Application.Health.HealthStatus.Healthy);
}

internal class FakeLlmHealthProbe : HydraForge.Application.Health.IHealthProbe
{
    public Task<HydraForge.Application.Health.HealthStatus> CheckAsync(CancellationToken ct = default)
        => Task.FromResult(HydraForge.Application.Health.HealthStatus.NotConfigured);
}
