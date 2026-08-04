using Microsoft.AspNetCore.Mvc.Testing;

namespace HydraForge.Server.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Test");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting(
                "Jwt:SigningKey",
                "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
            );
            builder.UseSetting("Llm:EncryptionKey", "0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo=");
            builder.UseSetting("AdminSeed:Username", "admin");
            builder.UseSetting("AdminSeed:Password", "a-real-generated-password-not-the-example");
        });

    [Fact]
    public async Task Response_AlwaysIncludesBaselineSecurityHeaders()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/Health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal(
            "strict-origin-when-cross-origin",
            response.Headers.GetValues("Referrer-Policy").Single()
        );
    }

    [Fact]
    public async Task Response_OverPlainHttp_DoesNotIncludeHsts()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/Health");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
