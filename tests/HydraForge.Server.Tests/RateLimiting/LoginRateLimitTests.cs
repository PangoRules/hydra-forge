using System.Net;
using System.Text;
using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Server.Tests.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Server.Tests.RateLimiting;

// Regression coverage for the Task 1 review finding: AddFixedWindowLimiter creates one
// counter shared by every caller, so one client exhausting the "Login" policy locked out
// every other IP for the rest of the window. Program.cs now partitions by client IP via
// AddPolicy + RateLimitPartition. WebApplicationFactory's TestServer reports the same
// loopback address for every request, so these tests inject a startup filter that lets a
// request set its own simulated Connection.RemoteIpAddress via a test-only header — the
// same mechanism the reviewer proved the bug with, just simulated instead of two real IPs.
public class LoginRateLimitTests
{
    private const string TestIpHeader = "X-Test-Client-Ip";

    [Fact]
    public async Task Login_SixthRequestFromSameIp_ReturnsTooManyRequests()
    {
        using var factory = new RateLimitWebApplicationFactory();
        using var client = factory.CreateClient();

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            HttpResponseMessage response = await SendLoginAsync(client, "203.0.113.10");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        HttpResponseMessage sixthResponse = await SendLoginAsync(client, "203.0.113.10");

        Assert.Equal(HttpStatusCode.TooManyRequests, sixthResponse.StatusCode);
    }

    [Fact]
    public async Task Login_DifferentIpAfterOtherIpExhaustsLimit_IsNotBlocked()
    {
        using var factory = new RateLimitWebApplicationFactory();
        using var client = factory.CreateClient();

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            await SendLoginAsync(client, "203.0.113.20");
        }
        HttpResponseMessage exhaustedIpResponse = await SendLoginAsync(client, "203.0.113.20");
        Assert.Equal(HttpStatusCode.TooManyRequests, exhaustedIpResponse.StatusCode);

        // A never-before-seen source IP must get its own budget, not share the exhausted one.
        HttpResponseMessage freshIpResponse = await SendLoginAsync(client, "203.0.113.21");

        Assert.Equal(HttpStatusCode.Unauthorized, freshIpResponse.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendLoginAsync(
        HttpClient client,
        string sourceIp
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(
                "{\"username\":\"admin\",\"password\":\"wrong\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add(TestIpHeader, sourceIp);
        return await client.SendAsync(request);
    }
}

internal class RateLimitWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting(
            "Jwt:SigningKey",
            "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
        );
        builder.ConfigureServices(services =>
        {
            foreach (
                var descriptor in services
                    .Where(d =>
                        d.ServiceType == typeof(IUserRepository)
                        || d.ServiceType == typeof(IPasswordHasher)
                        || d.ServiceType == typeof(IAccessTokenIssuer)
                        || d.ServiceType == typeof(IAuditLogWriter)
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IUserRepository>(_ => new AuthTestUserRepository(
                userDisabled: false
            ));
            services.AddSingleton<IPasswordHasher>(_ => new AuthTestPasswordHasher(
                passwordValid: false
            ));
            services.AddSingleton<IAccessTokenIssuer>(_ => new AuthTestTokenIssuer());
            // LoginUserHandler now audit-logs failed attempts — swap in the in-memory fake
            // so the handler doesn't try to reach a real Postgres instance.
            services.AddScoped<IAuditLogWriter>(_ => new InMemoryAuditLogWriter());

            // Runs before every other pipeline middleware (including UseRateLimiter), so the
            // simulated source IP is in place before the rate limiter reads the partition key.
            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
        });
    }
}

internal class TestClientIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(
                async (context, nextMiddleware) =>
                {
                    string? testIp = context.Request.Headers["X-Test-Client-Ip"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(testIp))
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(testIp);
                    }
                    await nextMiddleware();
                }
            );
            next(app);
        };
}
