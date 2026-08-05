namespace HydraForge.Server.Tests.Controllers;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HydraForge.Application.Llm;
using HydraForge.Application.ProjectDocuments;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class AccountControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string IssueUserToken(Guid userId)
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                userId.ToString()
            ),
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(
                "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
            )
        );
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256
        );
        return handler.CreateToken(
            new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = identity,
                Issuer = "HydraForge",
                Audience = "HydraForge",
                SigningCredentials = credentials,
                Expires = DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime,
            }
        );
    }

    [Fact]
    public async Task GetUsage_AuthenticatedUser_ReturnsOwnUsage()
    {
        var userId = Guid.NewGuid();
        var mock = Substitute.For<ILlmAdminService>();
        mock.GetAccountUsageAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<AccountUsageResponse>.Success(
                    new AccountUsageResponse(
                        5000,
                        100000,
                        10,
                        100,
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddMonths(1),
                        new List<RecentCallDto>
                        {
                            new("PersonalChat", "gpt-4o", 5000, 0, 0.05m, DateTime.UtcNow),
                        }
                    )
                )
            );

        using var factory = new AccountControllerTestFactory();
        factory.LlmAdmin = mock;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IssueUserToken(userId)
        );

        var response = await client.GetAsync("api/account/usage", CancellationToken.None);

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AccountUsageResponse>(
            JsonOptions,
            CancellationToken.None
        );
        Assert.NotNull(dto);
        Assert.Equal(5000, dto.TokensUsed);
        Assert.Equal(100000, dto.TokensBudget);
        Assert.Equal(10, dto.ImagesUsed);
        Assert.Equal(100, dto.ImagesBudget);
        Assert.Single(dto.RecentCalls);
        Assert.Equal("PersonalChat", dto.RecentCalls[0].Feature);
    }

    [Fact]
    public async Task GetUsage_Unauthenticated_Returns401()
    {
        using var factory = new AccountControllerTestFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("api/account/usage", CancellationToken.None);

        Assert.Equal(401, (int)response.StatusCode);
    }

    [Fact]
    public async Task GetUsage_EmptyUsage_ReturnsZerosAndEmptyRecentCalls()
    {
        var userId = Guid.NewGuid();
        var mock = Substitute.For<ILlmAdminService>();
        mock.GetAccountUsageAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<AccountUsageResponse>.Success(
                    new AccountUsageResponse(
                        0,
                        0,
                        0,
                        0,
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddMonths(1),
                        []
                    )
                )
            );

        using var factory = new AccountControllerTestFactory();
        factory.LlmAdmin = mock;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IssueUserToken(userId)
        );

        var response = await client.GetAsync("api/account/usage", CancellationToken.None);

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AccountUsageResponse>(
            JsonOptions,
            CancellationToken.None
        );
        Assert.NotNull(dto);
        Assert.Equal(0, dto.TokensUsed);
        Assert.Equal(0, dto.TokensBudget);
        Assert.Equal(0, dto.ImagesUsed);
        Assert.Equal(0, dto.ImagesBudget);
        Assert.Empty(dto.RecentCalls);
    }
}

internal class AccountControllerTestFactory : WebApplicationFactory<Program>
{
    public ILlmAdminService LlmAdmin { get; set; } = Substitute.For<ILlmAdminService>();

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
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(ILlmAdminService)
            );
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddScoped<ILlmAdminService>(_ => LlmAdmin);
            services.AddScoped<IProjectDocumentRepository>(_ => Substitute.For<IProjectDocumentRepository>());
            services.AddScoped<ProjectDocumentService>();
        });
    }
}
