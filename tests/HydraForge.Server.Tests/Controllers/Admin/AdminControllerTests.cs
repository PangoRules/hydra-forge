using System.Net;
using System.Net.Http.Headers;
using HydraForge.Application.Admin;
using HydraForge.Application.Auth;
using HydraForge.Domain.Entities.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Server.Tests.Controllers.Admin;

public class AdminControllerTests
{
    [Fact]
    public async Task ListUsers_Unauthenticated_Returns401()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_NonAdmin_Returns403()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_Admin_ReturnsOk()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(Guid.NewGuid(), "admin", isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

internal class AdminTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<User> _users = [];

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
                        d.ServiceType == typeof(IAdminService)
                        || d.ServiceType == typeof(IUserRepository)
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IUserRepository>(_ => new TestAdminUserRepository(_users));
            services.AddScoped<IPasswordHasher>(_ => new TestPasswordHasher());
            services.AddScoped<IAdminService, AdminService>();
        });
    }

    public static string IssueToken(Guid userId, string username, bool isAdmin)
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                userId.ToString()
            ),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username),
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Role,
                isAdmin ? "Admin" : "User"
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
}

internal class TestAdminUserRepository(List<User> users) : IUserRepository
{
    private readonly List<User> _users = users;

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, User>>(
            _users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id)
        );

    public Task<User?> FindByUsernameAsync(string username) =>
        Task.FromResult(
            _users.FirstOrDefault(u =>
                string.Equals(
                    u.UsernameNormalized,
                    username,
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
        );

    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
        IReadOnlyList<string> usernames,
        string? searchTerm = null,
        int maxResults = 10,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyDictionary<string, User>>(new Dictionary<string, User>());

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(_users.Any(u => u.IsAdmin));

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == userId)?.IsAdmin ?? false);

    public Task CreateAsync(User user, CancellationToken ct = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> ListAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    )
    {
        var query = _users.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
            );
        return Task.FromResult<IReadOnlyList<User>>([
            .. query.OrderBy(u => u.Username).Skip(skip).Take(take),
        ]);
    }

    public Task<int> CountAsync(string? search, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(search))
            return Task.FromResult(_users.Count);
        return Task.FromResult(
            _users.Count(u => u.Username.Contains(search, StringComparison.OrdinalIgnoreCase))
        );
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var idx = _users.FindIndex(u => u.Id == user.Id);
        if (idx >= 0)
            _users[idx] = user;
        return Task.CompletedTask;
    }
}

internal class TestPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => "hashed:" + password;

    public bool VerifyPassword(string password, string hash) => hash == "hashed:" + password;
}
