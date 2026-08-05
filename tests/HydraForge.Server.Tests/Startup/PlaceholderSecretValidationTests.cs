using Microsoft.AspNetCore.Mvc.Testing;

namespace HydraForge.Server.Tests.Startup;

public class PlaceholderSecretValidationTests
{
    [Fact]
    public void Startup_WithPlaceholderAdminSeedPassword_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Environment", "Test");
                builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
                builder.UseSetting(
                    "Jwt:SigningKey",
                    "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
                );
                builder.UseSetting(
                    "Llm:EncryptionKey",
                    "0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo="
                );
                builder.UseSetting("AdminSeed:Username", "admin");
                builder.UseSetting("AdminSeed:Password", "change-this-admin-password");
            });
            using var client = factory.CreateClient();
        });

        Assert.Contains("AdminSeed:Password", ex.Message);
    }

    [Fact]
    public void Startup_WithPlaceholderLlmEncryptionKey_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Environment", "Test");
                builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
                builder.UseSetting(
                    "Jwt:SigningKey",
                    "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
                );
                builder.UseSetting(
                    "Llm:EncryptionKey",
                    "ckaOH71rTlfT6cR0r28AObevMLQSJFCz4goqiV2aMwY="
                );
                builder.UseSetting("AdminSeed:Username", "admin");
                builder.UseSetting(
                    "AdminSeed:Password",
                    "a-real-generated-password-not-the-example"
                );
            });
            using var client = factory.CreateClient();
        });

        Assert.Contains("Llm:EncryptionKey", ex.Message);
    }
}
