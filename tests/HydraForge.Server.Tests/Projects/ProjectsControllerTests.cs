namespace HydraForge.Server.Tests.Projects;

using System.Net;
using System.Text;
using HydraForge.Application.Audit;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class ProjectsControllerTests
{
    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedProject()
    {
        var factory = new ProjectsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "admin", isAdmin: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/projects")
        {
            Content = new StringContent(
                "{\"name\":\"Test Project\",\"description\":\"A test\",\"gitRemoteUrl\":null,\"gitProvider\":null}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test Project", body);
        Assert.Contains("id", body);
    }

    [Fact]
    public async Task Create_TokenIssuedByJwtTokenIssuer_ReturnsCreatedProject()
    {
        var factory = new ProjectsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueApplicationToken(Guid.NewGuid(), "admin", isAdmin: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/projects")
        {
            Content = new StringContent(
                "{\"name\":\"JWT Project\",\"description\":\"A test\",\"gitRemoteUrl\":null,\"gitProvider\":null}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_TokenWithRawSubClaim_ReturnsCreatedProject()
    {
        var factory = new ProjectsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueRawSubToken(Guid.NewGuid(), "admin", isAdmin: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/projects")
        {
            Content = new StringContent(
                "{\"name\":\"Raw Sub Project\",\"description\":\"A test\",\"gitRemoteUrl\":null,\"gitProvider\":null}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_TokenWithoutUserIdClaim_ReturnsForbidden()
    {
        var factory = new ProjectsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueTokenWithoutUserId("admin", isAdmin: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/projects")
        {
            Content = new StringContent(
                "{\"name\":\"Missing User\",\"description\":\"A test\",\"gitRemoteUrl\":null,\"gitProvider\":null}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonMember_Returns403()
    {
        var factory = new ProjectsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Private Project" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Member_ReturnsProject()
    {
        var factory = new ProjectsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Shared Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Shared Project", body);
    }

    [Fact]
    public async Task Delete_NonOwner_Returns403()
    {
        var factory = new ProjectsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Delete Test" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_NonOwner_Returns403()
    {
        var factory = new ProjectsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var ownerToken = factory.IssueToken(ownerId, "owner", isAdmin: false);
        var memberToken = factory.IssueToken(memberId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Add Member Test" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = memberId, Role = MemberRole.Member });

        var newUserId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/members")
        {
            Content = new StringContent(
                $"{{\"userId\":\"{newUserId}\",\"role\":\"Member\"}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {memberToken}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

internal class ProjectsTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Jwt:SigningKey", "test-secret-key-that-is-at-least-32-chars-long-for-hs256");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d =>
                d.ServiceType == typeof(ProjectService)
                || d.ServiceType == typeof(IProjectRepository)
                || d.ServiceType == typeof(IColumnRepository)
                || d.ServiceType == typeof(IProjectMemberRepository)
                || d.ServiceType == typeof(IProjectContextSnapshotRepository)
                || d.ServiceType == typeof(IChatArchiveService)
                || d.ServiceType == typeof(HydraForge.Application.ProjectSnapshots.IProjectSnapshotRefresher)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IProjectRepository>(_ => new TestProjectRepository(_projects));
            services.AddScoped<IColumnRepository>(_ => new TestColumnRepository());
            services.AddScoped<IProjectMemberRepository>(_ => new TestProjectMemberRepository(_members));
            services.AddScoped<IProjectContextSnapshotRepository>(_ => new TestSnapshotRepository());
            services.AddScoped<IChatArchiveService>(_ => new TestChatArchiveService());
            services.AddScoped<HydraForge.Application.ProjectSnapshots.IProjectSnapshotRefresher>(_ => new TestSnapshotRefresher());
            services.AddScoped<HydraForge.Application.Realtime.IProjectBoardEventPublisher>(_ => new FakeProjectBoardEventPublisher());
            services.AddScoped<IAuditLogWriter>(_ => new InMemoryAuditLogWriter());
            services.AddScoped<ProjectService>();
            services.AddScoped<ProjectMemberService>();
        });
    }

    public void AddProject(Project project) => _projects.Add(project);
    public void AddMember(ProjectMember member) => _members.Add(member);

    public string IssueToken(Guid userId, string username, bool isAdmin)
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, isAdmin ? "Admin" : "User")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("test-secret-key-that-is-at-least-32-chars-long-for-hs256"));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = handler.CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = "HydraForge",
            Audience = "HydraForge",
            SigningCredentials = credentials,
           Expires = DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime
        });

        return token;
    }

    public string IssueApplicationToken(Guid userId, string username, bool isAdmin)
    {
        var issuer = new JwtTokenIssuer(
            "HydraForge",
            "HydraForge",
            "test-secret-key-that-is-at-least-32-chars-long-for-hs256",
            30
        );

        return issuer.IssueToken(new User
        {
            Id = userId,
            Username = username,
            IsAdmin = isAdmin,
        }).Value;
    }

    public string IssueRawSubToken(Guid userId, string username, bool isAdmin)
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim("sub", userId.ToString()),
            new System.Security.Claims.Claim("name", username),
            new System.Security.Claims.Claim("is_admin", isAdmin.ToString().ToLowerInvariant())
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("test-secret-key-that-is-at-least-32-chars-long-for-hs256"));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        return handler.CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = "HydraForge",
            Audience = "HydraForge",
            SigningCredentials = credentials,
            Expires = DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime
        });
    }

    public string IssueTokenWithoutUserId(string username, bool isAdmin)
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim("name", username),
            new System.Security.Claims.Claim("is_admin", isAdmin.ToString().ToLowerInvariant())
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("test-secret-key-that-is-at-least-32-chars-long-for-hs256"));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        return handler.CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = "HydraForge",
            Audience = "HydraForge",
            SigningCredentials = credentials,
            Expires = DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime
        });
    }
}

internal class TestProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects;

    public TestProjectRepository(List<Project> projects) => _projects = projects;

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _projects.Add(project);
        return Task.CompletedTask;
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<Project?>(_projects.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Project>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(_projects);

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var idx = _projects.FindIndex(p => p.Id == project.Id);
        if (idx >= 0) _projects[idx] = project;
        return Task.CompletedTask;
    }
}

internal class TestColumnRepository : IColumnRepository
{
    private readonly List<Column> _columns = [];

    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default)
    {
        _columns.AddRange(columns);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Column>>(_columns.Where(c => c.ProjectId == projectId).ToList());

    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_columns.FirstOrDefault(c => c.Id == id));

    public Task AddAsync(Column column, CancellationToken ct = default)
    {
        _columns.Add(column);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        var idx = _columns.FindIndex(c => c.Id == column.Id);
        if (idx >= 0) _columns[idx] = column;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _columns.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default)
    {
        for (var i = 0; i < orderedColumnIds.Count; i++)
        {
            var col = _columns.FirstOrDefault(c => c.Id == orderedColumnIds[i]);
            if (col != null) col.Position = i;
        }
        return Task.CompletedTask;
    }
}

internal class TestProjectMemberRepository : IProjectMemberRepository
{
    private readonly List<ProjectMember> _members;

    public TestProjectMemberRepository(List<ProjectMember> members) => _members = members;

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        _members.Add(member);
        return Task.CompletedTask;
    }

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_members.FirstOrDefault(m => m.Id == id));

    public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<ProjectMember?>(_members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));

    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProjectMember>>(_members.Where(m => m.ProjectId == projectId).ToList());

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
    {
        var idList = projectIds.ToList();
        var counts = _members
            .Where(m => idList.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }

    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
    {
        _members.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }

    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = _members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0) _members[idx] = member;
        return Task.CompletedTask;
    }
}

internal class TestSnapshotRepository : IProjectContextSnapshotRepository
{
    private readonly List<ProjectContextSnapshot> _snapshots = [];

    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        _snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<ProjectContextSnapshot?>(_snapshots.FirstOrDefault(s => s.ProjectId == projectId));
    public Task UpdateAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
}

internal class TestChatArchiveService : IChatArchiveService
{
    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.CompletedTask;
}
