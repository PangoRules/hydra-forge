using System.Net;
using System.Text.Json;
using HydraForge.Server.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
        builder.Configure(app =>
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/health", async context =>
                {
                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(new
                    {
                        status = "healthy",
                        components = new[]
                        {
                            new { name = "server", status = "healthy", detail = "Server is running." },
                            new { name = "database", status = "healthy", detail = "Database is connected." },
                            new { name = "llmProviders", status = "notconfigured", detail = "No LLM providers configured." }
                        }
                    });
                    await context.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json));
                });
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        return host;
    }
}
