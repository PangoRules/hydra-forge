using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HydraForge.Application.Admin;
using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.ProjectDocuments;
using HydraForge.Application.Settings;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.PersonalSpace;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

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
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "user",
            isAdmin: false
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_Admin_ReturnsOk()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "admin",
            isAdmin: true
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_NonAdmin_Returns403()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "user",
            isAdmin: false
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/settings");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_Admin_Returns200WithValidShape()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "admin",
            isAdmin: true
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<SettingsResponse>();
        Assert.NotNull(json);
        Assert.Equal(730, json.ArchivedItemRetentionDays);
        Assert.Equal(90, json.AuditLogRetentionDays);
        Assert.Equal(30, json.NotificationRetentionDays);
    }

    [Fact]
    public async Task UpdateSettings_Admin_PartialUpdate_Returns200()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "admin",
            isAdmin: true
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { archivedItemRetentionDays = 500 };
        using var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/admin/settings")
        {
            Content = JsonContent.Create(request),
        };
        var putResp = await client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var json = await putResp.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(json);
        Assert.Contains("5 minutes", json.Message);
    }

    [Fact]
    public async Task UpdateSettings_Admin_FullUpdate_Returns200()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "admin",
            isAdmin: true
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            archivedItemRetentionDays = 365,
            auditLogRetentionDays = 60,
            notificationRetentionDays = 14,
            ntfyServerUrl = "http://ntfy.example.com",
            searXngUrl = "http://search.example.com",
            brandName = "HydraForge",
            brandLogoUrl = "https://example.com/logo.png",
        };
        using var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/admin/settings")
        {
            Content = JsonContent.Create(request),
        };
        var response = await client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSettings_Admin_SetsHousekeepingRunTime_RoundTrips()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "admin",
            isAdmin: true
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { housekeepingRunTimeUtc = "04:30:00" };
        using var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/admin/settings")
        {
            Content = JsonContent.Create(request),
        };
        var putResp = await client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var getResp = await client.GetAsync("/api/admin/settings");
        var json = await getResp.Content.ReadFromJsonAsync<SettingsResponse>();
        Assert.NotNull(json);
        Assert.Equal(new TimeSpan(4, 30, 0), json.HousekeepingRunTimeUtc);
    }

    [Fact]
    public async Task GetAuditLog_Unauthenticated_Returns401()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/admin/audit-log");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditLog_NonAdmin_Returns403()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "user",
            isAdmin: false
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/audit-log");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditLog_Admin_ReturnsOkWithResult()
    {
        var factory = new AdminTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = AdminTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "admin",
            isAdmin: true
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/audit-log?entityType=Card&take=1000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<AuditLogQueryResultResponse>();
        Assert.NotNull(json);
        Assert.Equal("Card", TestAuditLogReader.LastQuery?.EntityType);
        Assert.Equal(500, TestAuditLogReader.LastQuery?.Take);
    }

    private record AuditLogQueryResultResponse(object[] Items, int TotalCount);

    private record SettingsResponse(
        int ArchivedItemRetentionDays,
        int AuditLogRetentionDays,
        int NotificationRetentionDays,
        string? NtfyServerUrl,
        string? SearXngUrl,
        string? BrandName,
        string? BrandLogoUrl,
        TimeSpan? HousekeepingRunTimeUtc
    );

    private record MessageResponse(string Message);
}

internal class AdminTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<User> _users = [];
    private readonly TestSettingsRepository _settingsRepo = new();

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
            foreach (
                var descriptor in services
                    .Where(d =>
                        d.ServiceType == typeof(IAdminService)
                        || d.ServiceType == typeof(IUserRepository)
                        || d.ServiceType == typeof(ISettingsRepository)
                        || d.ServiceType == typeof(ISettingsProvider)
                        || d.ServiceType == typeof(IAuditLogReader)
                        || d.ServiceType == typeof(IProjectDocumentRepository)
                        || d.ServiceType == typeof(ProjectDocumentService)
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IUserRepository>(_ => new TestAdminUserRepository(_users));
            services.AddScoped<IPasswordHasher>(_ => new TestPasswordHasher());
            services.AddScoped<IAuditLogWriter>(_ => new InMemoryAuditLogWriter());
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IProjectDocumentRepository>(_ =>
                Substitute.For<IProjectDocumentRepository>()
            );
            services.AddScoped<ProjectDocumentService>();
            services.AddScoped<ISettingsRepository>(_ => _settingsRepo);
            services.AddScoped<ISettingsProvider>(_ => new TestCachedSettingsProvider(
                _settingsRepo
            ));
            services.AddScoped<IAuditLogReader>(_ => new TestAuditLogReader());
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

internal class TestSettingsRepository : ISettingsRepository
{
    // Holds state across calls (a fresh SystemSettings() per GetSingletonAsync call meant
    // every PUT silently vanished on the next GET) — needed so tests can round-trip a
    // saved setting instead of only asserting on the PUT response.
    private SystemSettings _settings = new();

    public Task<SystemSettings> GetSingletonAsync(CancellationToken ct = default) =>
        Task.FromResult(_settings);

    public Task UpdateAsync(SystemSettings settings, CancellationToken ct = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}

internal class TestCachedSettingsProvider(ISettingsRepository repo) : ISettingsProvider
{
    public Task<SystemSettings> GetAsync(CancellationToken ct = default) =>
        repo.GetSingletonAsync(ct);

    public void Invalidate() { }
}

internal class TestAuditLogReader : IAuditLogReader
{
    public static AuditLogQuery? LastQuery { get; private set; }

    public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        LastQuery = query;
        return Task.FromResult(new AuditLogQueryResult([], 0));
    }
}
