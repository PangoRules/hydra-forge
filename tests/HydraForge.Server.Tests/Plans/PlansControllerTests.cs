namespace HydraForge.Server.Tests.Plans;

using System.Net;
using System.Text;
using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Plans;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Result = HydraForge.Domain.Common.Result;

public class PlansControllerTests
{
    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/plans")
        {
            Content = new StringContent(
                "{\"title\":\"My Plan\",\"description\":\"desc\",\"content\":\"# Plan\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("My Plan", body);
        Assert.Contains("\"version\":1", body);
    }

    [Fact]
    public async Task Create_NonMember_Returns403()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/plans")
        {
            Content = new StringContent(
                "{\"title\":\"P\",\"description\":null,\"content\":\"# P\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_Member_ReturnsPlans()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddPlan(new Plan { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Plan 1", Content = "#1", Version = 1, CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/plans");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Plan 1", body);
    }

    [Fact]
    public async Task GetById_ExistingPlan_ReturnsPlan()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddPlan(new Plan { Id = planId, ProjectId = projectId, Title = "Test Plan", Content = "# Content", Version = 1, CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/plans/{planId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test Plan", body);
        Assert.Contains("# Content", body);
    }

    [Fact]
    public async Task GetById_NonExistentPlan_Returns404()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/plans/{Guid.NewGuid()}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PLAN_NOT_FOUND", body);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsUpdatedPlan()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddPlan(new Plan { Id = planId, ProjectId = projectId, Title = "Original", Content = "V1", Version = 1, CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}/plans/{planId}")
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
    public async Task ListVersions_ExistingPlan_ReturnsVersions()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddPlan(new Plan { Id = planId, ProjectId = projectId, Title = "P", Content = "V2", Version = 2, CreatedByUserId = userId });
        factory.AddPlanVersion(new PlanVersion { Id = Guid.NewGuid(), PlanId = planId, Version = 1, Content = "V1", CreatedByUserId = userId });
        factory.AddPlanVersion(new PlanVersion { Id = Guid.NewGuid(), PlanId = planId, Version = 2, Content = "V2", CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/plans/{planId}/versions");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("V1", body);
        Assert.Contains("V2", body);
    }

    [Fact]
    public async Task Restore_ValidVersion_ReturnsRestoredPlan()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddPlan(new Plan { Id = planId, ProjectId = projectId, Title = "P", Content = "V2", Version = 2, CreatedByUserId = userId });
        factory.AddPlanVersion(new PlanVersion { Id = Guid.NewGuid(), PlanId = planId, Version = 1, Content = "V1", CreatedByUserId = userId });
        factory.AddPlanVersion(new PlanVersion { Id = Guid.NewGuid(), PlanId = planId, Version = 2, Content = "V2", CreatedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/plans/{planId}/restore")
        {
            Content = new StringContent("{\"version\":1}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"version\":3", body);
    }

    [Fact]
    public async Task LinkToCard_ValidCard_Returns204()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddPlan(new Plan { Id = planId, ProjectId = projectId, Title = "P", Content = "", Version = 1, CreatedByUserId = userId });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), Title = "C", CardNumber = 1 });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/plans/{planId}/link")
        {
            Content = new StringContent($"{{\"cardId\":\"{cardId}\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkFromCard_Returns204()
    {
        var factory = new PlansTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddPlan(new Plan { Id = planId, ProjectId = projectId, Title = "P", Content = "", Version = 1, CreatedByUserId = userId });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), PlanId = planId, Title = "C", CardNumber = 1 });

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/plans/{planId}/link/{cardId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

internal class PlansTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];
    private readonly List<Plan> _plans = [];
    private readonly List<PlanVersion> _planVersions = [];
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
                || d.ServiceType == typeof(PlanService)
                || d.ServiceType == typeof(IProjectRepository)
                || d.ServiceType == typeof(IPlanRepository)
                || d.ServiceType == typeof(ICardRepository)
                || d.ServiceType == typeof(IProjectMemberRepository)
                || d.ServiceType == typeof(IProjectContextSnapshotRepository)
                || d.ServiceType == typeof(IChatArchiveService)
                || d.ServiceType == typeof(IColumnRepository)
                || d.ServiceType == typeof(ICardAssigneeRepository)
                || d.ServiceType == typeof(ICardWatcherRepository)
                || d.ServiceType == typeof(ICardRelationshipRepository)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IProjectRepository>(_ => new PlansTestProjectRepository(_projects));
            services.AddScoped<IPlanRepository>(_ => new PlansTestPlanRepository(_plans, _planVersions, _cards));
            services.AddScoped<ICardRepository>(_ => new PlansTestCardRepository(_cards));
            services.AddScoped<IProjectMemberRepository>(_ => new PlansTestMemberRepository(_members));
            services.AddScoped<IProjectContextSnapshotRepository>(_ => new PlansTestSnapshotRepository());
            services.AddScoped<IChatArchiveService>(_ => new PlansTestChatArchiveService());
            services.AddScoped<IAuditLogWriter>(_ => new PlansTestAuditLogWriter());
            services.AddScoped<ProjectService>();
            services.AddScoped<PlanService>();
        });
    }

    public void AddProject(Project project) => _projects.Add(project);
    public void AddMember(ProjectMember member) => _members.Add(member);
    public void AddPlan(Plan plan) => _plans.Add(plan);
    public void AddPlanVersion(PlanVersion pv) => _planVersions.Add(pv);
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

internal class PlansTestProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects;

    public PlansTestProjectRepository(List<Project> projects) => _projects = projects;

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

internal class PlansTestPlanRepository : IPlanRepository
{
    private readonly List<Plan> _plans;
    private readonly List<PlanVersion> _versions;
    private readonly List<Card> _cards;

    public PlansTestPlanRepository(List<Plan> plans, List<PlanVersion> versions, List<Card> cards)
    {
        _plans = plans;
        _versions = versions;
        _cards = cards;
    }

    public Task<Plan?> GetByIdAsync(Guid planId, CancellationToken ct = default)
        => Task.FromResult(_plans.FirstOrDefault(p => p.Id == planId));
    public Task<IReadOnlyList<Plan>> ListByProjectAsync(Guid projectId, PlanListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Plan>>(_plans.Where(p => p.ProjectId == projectId).ToList());
    public Task<PlanVersion?> GetVersionAsync(Guid planId, int version, CancellationToken ct = default)
        => Task.FromResult(_versions.FirstOrDefault(v => v.PlanId == planId && v.Version == version));
    public Task<IReadOnlyList<PlanVersion>> ListVersionsAsync(Guid planId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PlanVersion>>(_versions.Where(v => v.PlanId == planId).OrderBy(v => v.Version).ToList());
    public Task<IReadOnlyDictionary<Guid, Guid?>> GetLinkedCardIdsAsync(Guid projectId, CancellationToken ct = default)
    {
        var ids = _cards.Where(c => c.PlanId != null && c.ProjectId == projectId).Select(c => new { c.PlanId, c.Id }).ToList();
        return Task.FromResult<IReadOnlyDictionary<Guid, Guid?>>(ids.ToDictionary(x => x.PlanId!.Value, x => (Guid?)x.Id));
    }
    public Task<Guid?> GetLinkedCardIdAsync(Guid planId, CancellationToken ct = default)
        => Task.FromResult<Guid?>(_cards.FirstOrDefault(c => c.PlanId == planId)?.Id);
    public Task AddAsync(Plan plan, CancellationToken ct = default) { _plans.Add(plan); return Task.CompletedTask; }
    public Task AddVersionAsync(PlanVersion version, CancellationToken ct = default) { _versions.Add(version); return Task.CompletedTask; }
    public Task UpdateAsync(Plan plan, CancellationToken ct = default)
    {
        var idx = _plans.FindIndex(p => p.Id == plan.Id);
        if (idx >= 0) _plans[idx] = plan;
        return Task.CompletedTask;
    }
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
}

internal class PlansTestCardRepository : ICardRepository
{
    private readonly List<Card> _cards;

    public PlansTestCardRepository(List<Card> cards) => _cards = cards;

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

internal class PlansTestMemberRepository : IProjectMemberRepository
{
    private readonly List<ProjectMember> _members;

    public PlansTestMemberRepository(List<ProjectMember> members) => _members = members;

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

internal class PlansTestSnapshotRepository : IProjectContextSnapshotRepository
{
    private readonly List<ProjectContextSnapshot> _snapshots = [];
    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default) { _snapshots.Add(snapshot); return Task.CompletedTask; }
    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<ProjectContextSnapshot?>(_snapshots.FirstOrDefault(s => s.ProjectId == projectId));
}

internal class PlansTestChatArchiveService : IChatArchiveService
{
    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
}

internal class PlansTestAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
        => Task.FromResult(Result.Success());
}
