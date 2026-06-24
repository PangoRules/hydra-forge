namespace HydraForge.Server.Tests.Specs;

using System.Net;
using System.Text;
using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Plans;
using HydraForge.Application.Projects;
using HydraForge.Application.Specs;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Result = HydraForge.Domain.Common.Result;

public class SpecsControllerTests
{
    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var factory = new SpecsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), Title = "Test Card", CardNumber = 1 });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/specs/cards/{cardId}")
        {
            Content = new StringContent(
                "{\"title\":\"My Spec\",\"description\":\"desc\",\"content\":\"# Spec\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("My Spec", body);
        Assert.Contains("\"version\":1", body);
    }

    [Fact]
    public async Task Create_NonMember_Returns403()
    {
        var factory = new SpecsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });

        var cardId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/specs/cards/{cardId}")
        {
            Content = new StringContent(
                "{\"title\":\"S\",\"description\":null,\"content\":\"# S\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_Member_ReturnsSpecs()
    {
        var factory = new SpecsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), Title = "Card", CardNumber = 1 });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddSpec(new Spec { Id = Guid.NewGuid(), CardId = cardId, ProjectId = projectId, Title = "Spec 1", Content = "#1", Version = 1, CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/specs/cards/{cardId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Spec 1", body);
    }

    [Fact]
    public async Task GetById_ExistingSpec_ReturnsSpec()
    {
        var factory = new SpecsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), Title = "Card", CardNumber = 1 });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddSpec(new Spec { Id = specId, CardId = cardId, ProjectId = projectId, Title = "Test Spec", Content = "# Content", Version = 1, CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/specs/{specId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test Spec", body);
        Assert.Contains("# Content", body);
    }

    [Fact]
    public async Task GetById_NonExistentSpec_Returns404()
    {
        var factory = new SpecsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/specs/{Guid.NewGuid()}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPEC_NOT_FOUND", body);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsUpdatedSpec()
    {
        var factory = new SpecsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), Title = "Card", CardNumber = 1 });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddSpec(new Spec { Id = specId, CardId = cardId, ProjectId = projectId, Title = "Original", Content = "V1", Version = 1, CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}/specs/{specId}")
        {
            Content = new StringContent(
                "{\"title\":\"Updated\",\"description\":null,\"content\":\"V2\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Updated", body);
        Assert.Contains("\"version\":2", body);
    }

    [Fact]
    public async Task ListVersions_ExistingSpec_ReturnsVersions()
    {
        var factory = new SpecsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), Title = "Card", CardNumber = 1 });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddSpec(new Spec { Id = specId, CardId = cardId, ProjectId = projectId, Title = "S", Content = "V2", Version = 2, CreatedByUserId = userId });
        factory.AddSpecVersion(new SpecVersion { Id = Guid.NewGuid(), SpecId = specId, Version = 1, Content = "V1", CreatedByUserId = userId });
        factory.AddSpecVersion(new SpecVersion { Id = Guid.NewGuid(), SpecId = specId, Version = 2, Content = "V2", CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/specs/{specId}/versions");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("V1", body);
        Assert.Contains("V2", body);
    }

    [Fact]
    public async Task Restore_ValidVersion_ReturnsRestoredSpec()
    {
        var factory = new SpecsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), Title = "Card", CardNumber = 1 });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddSpec(new Spec { Id = specId, CardId = cardId, ProjectId = projectId, Title = "S", Content = "V2", Version = 2, CreatedByUserId = userId });
        factory.AddSpecVersion(new SpecVersion { Id = Guid.NewGuid(), SpecId = specId, Version = 1, Content = "V1", CreatedByUserId = userId });
        factory.AddSpecVersion(new SpecVersion { Id = Guid.NewGuid(), SpecId = specId, Version = 2, Content = "V2", CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/specs/{specId}/restore")
        {
            Content = new StringContent("{\"version\":1}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"version\":3", body);
    }
}

internal class SpecsTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];
    private readonly List<Spec> _specs = [];
    private readonly List<SpecVersion> _specVersions = [];
    private readonly List<Card> _cards = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Jwt:SigningKey", "test-secret-key-that-is-at-least-32-chars-long-for-hs256");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d =>
                d.ServiceType == typeof(ProjectService)
                || d.ServiceType == typeof(SpecService)
                || d.ServiceType == typeof(PlanService)
                || d.ServiceType == typeof(IProjectRepository)
                || d.ServiceType == typeof(ISpecRepository)
                || d.ServiceType == typeof(IPlanRepository)
                || d.ServiceType == typeof(ICardRepository)
                || d.ServiceType == typeof(IProjectMemberRepository)
                || d.ServiceType == typeof(IProjectContextSnapshotRepository)
                || d.ServiceType == typeof(IChatArchiveService)
                || d.ServiceType == typeof(HydraForge.Application.ProjectSnapshots.IProjectSnapshotRefresher)
                || d.ServiceType == typeof(IColumnRepository)
                || d.ServiceType == typeof(ICardAssigneeRepository)
                || d.ServiceType == typeof(ICardWatcherRepository)
                || d.ServiceType == typeof(ICardRelationshipRepository)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IProjectRepository>(_ => new SpecsTestProjectRepository(_projects));
            services.AddScoped<ISpecRepository>(_ => new SpecsTestSpecRepository(_specs, _specVersions, _cards));
            services.AddScoped<ICardRepository>(_ => new SpecsTestCardRepository(_cards));
            services.AddScoped<IProjectMemberRepository>(_ => new SpecsTestMemberRepository(_members));
            services.AddScoped<IProjectContextSnapshotRepository>(_ => new SpecsTestSnapshotRepository());
            services.AddScoped<IChatArchiveService>(_ => new SpecsTestChatArchiveService());
            services.AddScoped<IAuditLogWriter>(_ => new SpecsTestAuditLogWriter());
            services.AddScoped<HydraForge.Application.ProjectSnapshots.IProjectSnapshotRefresher>(_ => new TestSnapshotRefresher());
            services.AddScoped<HydraForge.Application.Realtime.IProjectBoardEventPublisher>(_ => new FakeProjectBoardEventPublisher());
            services.AddScoped<ProjectService>();
            services.AddScoped<SpecService>();
        });
    }

    public void AddProject(Project project) => _projects.Add(project);
    public void AddMember(ProjectMember member) => _members.Add(member);
    public void AddSpec(Spec spec) => _specs.Add(spec);
    public void AddSpecVersion(SpecVersion sv) => _specVersions.Add(sv);
    public void AddCard(Card card) => _cards.Add(card);

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
}

internal class SpecsTestProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects;

    public SpecsTestProjectRepository(List<Project> projects) => _projects = projects;

    public Task AddAsync(Project project, CancellationToken ct = default) { _projects.Add(project); return Task.CompletedTask; }
    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_projects.FirstOrDefault(p => p.Id == id));
    public Task<IReadOnlyList<Project>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(_projects);
    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var idx = _projects.FindIndex(p => p.Id == project.Id);
        if (idx >= 0) _projects[idx] = project;
        return Task.CompletedTask;
    }
}

internal class SpecsTestSpecRepository : ISpecRepository
{
    private readonly List<Spec> _specs;
    private readonly List<SpecVersion> _versions;
    private readonly List<Card> _cards;

    public SpecsTestSpecRepository(List<Spec> specs, List<SpecVersion> versions, List<Card> cards)
    {
        _specs = specs;
        _versions = versions;
        _cards = cards;
    }

    public Task<Spec?> GetByIdAsync(Guid specId, CancellationToken ct = default)
        => Task.FromResult(_specs.FirstOrDefault(s => s.Id == specId));
    public Task<IReadOnlyList<Spec>> ListByProjectAsync(Guid projectId, SpecListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Spec>>(_specs.Where(s => s.ProjectId == projectId).ToList());
    public Task<IReadOnlyList<Spec>> ListByCardAsync(Guid cardId, SpecListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Spec>>(_specs.Where(s => s.CardId == cardId).ToList());
    public Task<SpecVersion?> GetVersionAsync(Guid specId, int version, CancellationToken ct = default)
        => Task.FromResult(_versions.FirstOrDefault(v => v.SpecId == specId && v.Version == version));
    public Task<IReadOnlyList<SpecVersion>> ListVersionsAsync(Guid specId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SpecVersion>>(_versions.Where(v => v.SpecId == specId).OrderBy(v => v.Version).ToList());
    public Task AddAsync(Spec spec, CancellationToken ct = default) { _specs.Add(spec); return Task.CompletedTask; }
    public Task AddVersionAsync(SpecVersion version, CancellationToken ct = default) { _versions.Add(version); return Task.CompletedTask; }
    public Task UpdateAsync(Spec spec, CancellationToken ct = default)
    {
        var idx = _specs.FindIndex(s => s.Id == spec.Id);
        if (idx >= 0) _specs[idx] = spec;
        return Task.CompletedTask;
    }
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
}

internal class SpecsTestCardRepository : ICardRepository
{
    private readonly List<Card> _cards;

    public SpecsTestCardRepository(List<Card> cards) => _cards = cards;

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(_cards.FirstOrDefault(c => c.Id == cardId));
    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(_cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));
    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult(_cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber));
    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Card>>(_cards.Where(c => c.ProjectId == projectId).ToList());
    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult(_cards.Where(c => c.ProjectId == projectId).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());
    public Task AddAsync(Card card, CancellationToken ct = default) { _cards.Add(card); return Task.CompletedTask; }
    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = _cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0) _cards[idx] = card;
        return Task.CompletedTask;
    }
    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default) { foreach (var c in cards) { var idx = _cards.FindIndex(x => x.Id == c.Id); if (idx >= 0) _cards[idx] = c; } return Task.CompletedTask; }
    public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { _cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default)
    {
        var toCompact = _cards.Where(c => c.ColumnId == columnId && c.Position > exceptPosition && c.ArchivedAt == null).ToList();
        foreach (var c in toCompact) c.Position -= 1;
        return Task.CompletedTask;
    }
    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
        => Task.FromResult(_cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
}

internal class SpecsTestMemberRepository : IProjectMemberRepository
{
    private readonly List<ProjectMember> _members;

    public SpecsTestMemberRepository(List<ProjectMember> members) => _members = members;

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_members.FirstOrDefault(m => m.Id == id));
    public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(_members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));
    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProjectMember>>(_members.Where(m => m.ProjectId == projectId).ToList());
    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
    {
        var idList = projectIds.ToList();
        var counts = _members.Where(m => idList.Contains(m.ProjectId)).GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }
    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) { _members.Add(member); return Task.CompletedTask; }
    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = _members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0) _members[idx] = member;
        return Task.CompletedTask;
    }
    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { _members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
}

internal class SpecsTestSnapshotRepository : IProjectContextSnapshotRepository
{
    private readonly List<ProjectContextSnapshot> _snapshots = [];
    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default) { _snapshots.Add(snapshot); return Task.CompletedTask; }
    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<ProjectContextSnapshot?>(_snapshots.FirstOrDefault(s => s.ProjectId == projectId));
    public Task UpdateAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
}

internal class SpecsTestChatArchiveService : IChatArchiveService
{
    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
}

internal class SpecsTestAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
        => Task.FromResult(Result.Success());
}
