using System.Net;
using HydraForge.Server.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace HydraForge.Server.Tests.Errors;

public class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task Middleware_Returns500WithCorrelationId()
    {
        var factory = new TestWebApplicationFactory();

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("stack", body.ToLowerInvariant());
    }

    [Fact]
    public async Task Middleware_ReturnsProblemContentType()
    {
        var factory = new TestWebApplicationFactory();

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/throw");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/throw", context => throw new InvalidOperationException("boom"));
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Bypass Program.Main by using IHostBuilder directly.
        // WebApplicationFactory default path calls Program.Main which runs migrations.
        var host = base.CreateHost(builder);
        return host;
    }
}