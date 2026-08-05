using NSubstitute;
namespace HydraForge.Server.Tests.Projects;

using System.Net;
using HydraForge.Application.Audit;
using HydraForge.Application.Projects;
using HydraForge.Application.ProjectDocuments;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class ProjectSnapshotEndpointTests
{
    [Fact]
    public async Task Get_NonMember_Returns403()
    {
        var factory = new SnapshotTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = SnapshotTestWebApplicationFactory.IssueToken(
            Guid.NewGuid(),
            "user",
            isAdmin: false
        );

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}/ProjectSnapshot"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_Member_ReturnsSnapshot()
    {
        var factory = new SnapshotTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = SnapshotTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddColumn(
            new Column
            {
                Id = columnId,
                ProjectId = projectId,
                Name = "Backlog",
                Position = 0,
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}/ProjectSnapshot"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Backlog", body);
    }

    [Fact]
    public async Task Get_InvalidProject_Returns403()
    {
        var factory = new SnapshotTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = SnapshotTestWebApplicationFactory.IssueToken(userId, "user", isAdmin: false);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{Guid.NewGuid()}/ProjectSnapshot"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

internal class SnapshotTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];
    private readonly List<Column> _columns = [];
    private readonly List<ProjectContextSnapshot> _snapshots = [];

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
            // Only replace the specific services the controller depends on.
            // Do NOT strip by namespace — that removes controllers.
            foreach (
                var descriptor in services
                    .Where(d =>
                        d.ServiceType == typeof(IProjectMemberRepository)
                        || d.ServiceType == typeof(IProjectSnapshotRefresher)
                        || d.ServiceType == typeof(IProjectContextSnapshotRepository)
                        || d.ServiceType == typeof(IProjectDocumentRepository)
                        || d.ServiceType == typeof(ProjectDocumentService)
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IProjectMemberRepository>(_ => new SnapTestMemberRepository(
                _members
            ));
            services.AddScoped<IProjectContextSnapshotRepository>(
                _ => new SnapTestSnapshotRepository(_snapshots)
            );
            services.AddScoped<IProjectSnapshotRefresher>(_ => new SnapTestSnapshotRefresher(
                _snapshots
            ));
            services.AddScoped<IAuditLogWriter>(_ => new InMemoryAuditLogWriter());
            services.AddScoped<IProjectDocumentRepository>(_ => Substitute.For<IProjectDocumentRepository>());
            services.AddScoped<ProjectDocumentService>();
        });
    }

    public void AddProject(Project p) => _projects.Add(p);

    public void AddMember(ProjectMember m) => _members.Add(m);

    public void AddColumn(Column c) => _columns.Add(c);

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
        var token = handler.CreateToken(
            new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = identity,
                Issuer = "HydraForge",
                Audience = "HydraForge",
                SigningCredentials = credentials,
                Expires = DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime,
            }
        );
        return token;
    }
}

internal class SnapTestMemberRepository(List<ProjectMember> members) : IProjectMemberRepository
{
    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(members.FirstOrDefault(m => m.Id == id));

    public Task<ProjectMember?> GetByProjectAndUserAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId)
        );

    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
        Guid projectId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<ProjectMember>>([
            .. members.Where(m => m.ProjectId == projectId),
        ]);

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken ct = default
    )
    {
        var counts = members
            .Where(m => projectIds.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }

    public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
        IEnumerable<Guid> projectIds,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        var roles = members
            .Where(m => idList.Contains(m.ProjectId) && m.UserId == userId)
            .ToDictionary(m => m.ProjectId, m => m.Role);
        return Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(roles);
    }

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        members.Add(member);
        return Task.CompletedTask;
    }

    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0)
            members[idx] = member;
        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
    {
        members.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }
}

internal class SnapTestSnapshotRepository(List<ProjectContextSnapshot> snapshots)
    : IProjectContextSnapshotRepository
{
    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken ct = default
    ) => Task.FromResult(snapshots.FirstOrDefault(s => s.ProjectId == projectId));

    public Task UpdateAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        var idx = snapshots.FindIndex(s => s.Id == snapshot.Id);
        if (idx >= 0)
            snapshots[idx] = snapshot;
        else
            snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProjectContextSnapshot>> GetByProjectIdsAsync(
        IReadOnlyList<Guid> projectIds,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<ProjectContextSnapshot>>([
            .. snapshots.Where(s => projectIds.Contains(s.ProjectId)),
        ]);

    public Task UpdateRangeAsync(
        IReadOnlyList<ProjectContextSnapshot> snapshots,
        CancellationToken ct = default
    ) => Task.CompletedTask;
}

internal class SnapTestSnapshotRefresher(List<ProjectContextSnapshot> snapshots)
    : IProjectSnapshotRefresher
{
    public Task RefreshAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<ProjectContextSnapshot?> GetSnapshotAsync(
        Guid projectId,
        CancellationToken ct = default
    )
    {
        var template =
            snapshots.FirstOrDefault(s => s.ProjectId == projectId)?.TemplateContent
            ?? "{\"columns\":[{\"name\":\"Backlog\",\"cards\":[]}]}";
        var snapshot = new ProjectContextSnapshot
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TemplateContent = template,
            TemplateGeneratedAt = DateTime.UtcNow,
        };
        snapshots.Add(snapshot);
        return Task.FromResult<ProjectContextSnapshot?>(snapshot);
    }
}
