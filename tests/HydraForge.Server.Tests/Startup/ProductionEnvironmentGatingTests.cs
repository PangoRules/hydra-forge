using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HydraForge.Server.Tests.Startup;

public class ProductionEnvironmentGatingTests
{
    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", environment);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("Hangfire:Enabled", "false");
            builder.UseSetting(
                "Jwt:SigningKey",
                "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
            );
            builder.UseSetting("Llm:EncryptionKey", "0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo=");
            builder.UseSetting("AdminSeed:Username", "admin");
            builder.UseSetting("AdminSeed:Password", "a-real-generated-password-not-the-example");
        });

    // "Test" (not "Production") on purpose — this repo's Hangfire/Test-env gating
    // means only "Test" skips the real-Postgres requirement AddHangfire has. The
    // Scalar/OpenAPI/TestUserSeeder gate this test cares about is IsDevelopment(),
    // which "Test" also fails, so it stands in for "Production" without needing a
    // live database connection.
    [Fact]
    public async Task NonDevelopmentEnvironment_ScalarAndOpenApiAreNotMapped()
    {
        using var factory = CreateFactory("Test");
        using var client = factory.CreateClient();

        var scalarResponse = await client.GetAsync("/scalar/v1");
        var openApiResponse = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, scalarResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, openApiResponse.StatusCode);
    }

    // Proves the assertions above are actually meaningful (not a false negative from
    // e.g. routing being broken in general) — under Development, both are mapped.
    [Fact]
    public async Task DevelopmentEnvironment_OpenApiIsMapped()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var openApiResponse = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
    }
}
