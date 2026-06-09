using System.Net;
using System.Text;
using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Server.Tests.Auth;

public class AuthEndpointTests
{
    [Fact]
    public async Task Login_InvalidCredentials_ReturnsProblemDetailsWithCorrelationAndCode()
    {
        var factory = new AuthWebApplicationFactory(userDisabled: false, passwordValid: false);
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(
                "{\"username\":\"admin\",\"password\":\"wrong\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("X-Correlation-Id", "auth-corr-1");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("auth-corr-1", body);
        Assert.Contains(DomainErrorCodes.Auth.InvalidCredentials, body);
    }

    [Fact]
    public async Task Login_DisabledUser_ReturnsForbiddenProblemDetails()
    {
        var factory = new AuthWebApplicationFactory(userDisabled: true, passwordValid: true);
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(
                "{\"username\":\"admin\",\"password\":\"password\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("X-Correlation-Id", "auth-corr-2");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("auth-corr-2", body);
        Assert.Contains(DomainErrorCodes.Auth.UserDisabled, body);
    }
}

internal class AuthWebApplicationFactory(bool userDisabled, bool passwordValid) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d =>
                d.ServiceType == typeof(IUserRepository)
                || d.ServiceType == typeof(IPasswordHasher)
                || d.ServiceType == typeof(IAccessTokenIssuer)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IUserRepository>(_ => new AuthTestUserRepository(userDisabled));
            services.AddSingleton<IPasswordHasher>(_ => new AuthTestPasswordHasher(passwordValid));
            services.AddSingleton<IAccessTokenIssuer>(_ => new AuthTestTokenIssuer());
        });
    }
}

internal class AuthTestUserRepository(bool userDisabled) : IUserRepository
{
    private readonly User _user = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Username = "admin",
        PasswordHash = "hashed",
        IsAdmin = true,
        IsDisabled = userDisabled,
    };

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(_user);

    public Task<User?> FindByUsernameAsync(string username) => Task.FromResult<User?>(_user);

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(true);

    public Task CreateAsync(User user) => Task.CompletedTask;
}

internal class AuthTestPasswordHasher(bool passwordValid) : IPasswordHasher
{
    public string HashPassword(string password) => "hashed";

    public bool VerifyPassword(string password, string hash) => passwordValid;
}

internal class AuthTestTokenIssuer : IAccessTokenIssuer
{
    public AccessToken IssueToken(User user) => new("jwt-token", DateTimeOffset.UtcNow.AddMinutes(30));
}
