using System.Net;
using HydraForge.Application.Health;
using HydraForge.Application.ProjectDocuments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

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
        builder.UseSetting(
            "Jwt:SigningKey",
            "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
        );
        builder.UseSetting("Llm:EncryptionKey", "0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo=");
        builder.ConfigureServices(services =>
        {
            // Remove existing IHealthProbe registrations (singleton GetHealthHandler already constructed with them)
            var removeDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(IHealthProbe)
                    || d.ServiceType == typeof(IProjectDocumentRepository)
                    || d.ServiceType == typeof(ProjectDocumentService)
                )
                .ToList();
            foreach (var d in removeDescriptors)
                services.Remove(d);

            // Add test fakes
            services.AddScoped<IHealthProbe, FakeServerHealthProbe>();
            services.AddScoped<IHealthProbe, FakeDbHealthProbe>();
            services.AddScoped<IHealthProbe, FakeLlmHealthProbe>();
            services.AddScoped<IProjectDocumentRepository>(_ =>
                Substitute.For<IProjectDocumentRepository>()
            );
            services.AddScoped<ProjectDocumentService>();
        });
    }
}

internal class FakeServerHealthProbe : IHealthProbe
{
    public Task<HealthStatus> CheckAsync(CancellationToken ct = default) =>
        Task.FromResult(HealthStatus.Healthy);
}

internal class FakeDbHealthProbe : IHealthProbe
{
    public Task<HealthStatus> CheckAsync(CancellationToken ct = default) =>
        Task.FromResult(HealthStatus.Healthy);
}

internal class FakeLlmHealthProbe : IHealthProbe
{
    public Task<HealthStatus> CheckAsync(CancellationToken ct = default) =>
        Task.FromResult(HealthStatus.NotConfigured);
}
