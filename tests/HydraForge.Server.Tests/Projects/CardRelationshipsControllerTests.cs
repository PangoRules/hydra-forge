namespace HydraForge.Server.Tests.Projects;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HydraForge.Application.Cards;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class CardRelationshipsControllerTests
{
    [Fact]
    public async Task List_NonMember_Returns403()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddColumn(new Column { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), CardNumber = 1, Title = "Card" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardId}/cardrelationships");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task List_Member_ReturnsRelationships()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card B" });

        // Create: cardA blocks cardB
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        // List
        var listReq = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships");
        listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResp = await client.SendAsync(listReq);

        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadAsStringAsync();
        Assert.Contains("Card B", body);
    }

    [Fact]
    public async Task Create_Duplicate_Returns409()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card B" });

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp1 = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        // New request with same payload (duplicate)
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp2 = await client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task Create_Cycle_Returns400()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card B" });

        // cardA blocks cardB
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp1 = await client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        // cardB blocks cardA → cycle
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardB}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardA.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp2 = await client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.BadRequest, resp2.StatusCode);
        var body = await resp2.Content.ReadAsStringAsync();
        Assert.Contains("cycle", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_CrossProject_Returns400()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddProject(new Project { Id = otherProjectId, Name = "Other Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = otherProjectId, ColumnId = colId, CardNumber = 1, Title = "Card B" });

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("cross", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Relates_Success()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card B" });

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 3 }), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card" });

        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/cards/{cardId}/cardrelationships/{Guid.NewGuid()}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_Success_Returns204()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card B" });

        // Create: cardA blocks cardB
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var createdBody = await createResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createdBody);
        var relId = doc.RootElement.GetProperty("id").GetString();

        // Delete
        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships/{relId}");
        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleteResp = await client.SendAsync(deleteReq);

        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task ArchiveImpact_Preflight_Returns200WithDependents()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card B" });

        // cardA blocks cardB
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        // GET preflight always returns 200 with the dependent card list
        var impactReq = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships/archive-impact");
        impactReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var impactResp = await client.SendAsync(impactReq);

        Assert.Equal(HttpStatusCode.OK, impactResp.StatusCode);
    }

    [Fact]
    public async Task ArchiveWithRelationships_Returns409WhenNotConfirmed()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card B" });

        // cardA blocks cardB
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        // POST archive-with-relationships without confirm → 409
        var archiveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships/archive-with-relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { confirm = false }), Encoding.UTF8, "application/json")
        };
        archiveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var archiveResp = await client.SendAsync(archiveReq);

        Assert.Equal(HttpStatusCode.Conflict, archiveResp.StatusCode);
    }

    [Fact]
    public async Task ArchiveWithRelationships_Success()
    {
        var factory = new CRTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var colId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardA, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card A" });
        factory.AddCard(new Card { Id = cardB, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card B" });

        // cardA blocks cardB
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        // Archive with relationships
        var archiveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/cardrelationships/archive-with-relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { confirm = true }), Encoding.UTF8, "application/json")
        };
        archiveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var archiveResp = await client.SendAsync(archiveReq);

        Assert.Equal(HttpStatusCode.OK, archiveResp.StatusCode);
    }
}

// ─── Test factory ─────────────────────────────────────────────────────────────

// Inherits all infrastructure from CardsTestWebApplicationFactory
// Only needs to override ICardRelationshipRepository to use shared _relationships list
// Extends CardsTestWebApplicationFactory with shared relationship storage
// CardsTestCardRelationshipRepository uses per-request scoped lists,
// so relationships created via POST don't persist to GET requests.
// We override with a repository backed by a shared list.
internal class CRTestWebApplicationFactory : CardsTestWebApplicationFactory
{
    private readonly List<CardRelationship> _relationships = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            // Remove the parent's per-scope relationship repo
            var existing = services.SingleOrDefault(d => d.ServiceType == typeof(ICardRelationshipRepository));
            if (existing != null) services.Remove(existing);
            // Replace with shared-list version
            services.AddScoped<ICardRelationshipRepository>(_ => new SharedCardRelationshipRepository(_relationships));
        });
    }
}

internal class SharedCardRelationshipRepository : ICardRelationshipRepository
{
    private readonly List<CardRelationship> _relationships;
    public SharedCardRelationshipRepository(List<CardRelationship> relationships) => _relationships = relationships;

    public Task<IReadOnlyList<CardRelationship>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null).ToList());
    public Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => r.TargetCardId == cardId && r.Type == RelationshipType.BlockedBy && r.ArchivedAt == null).ToList());
    public Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => r.SourceCardId == cardId && r.Type == RelationshipType.Precedes && r.ArchivedAt == null).ToList());
    public Task<CardRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<CardRelationship?>(_relationships.FirstOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<CardRelationship>> ListActiveByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => (r.SourceCardId == cardId || r.TargetCardId == cardId) && r.ArchivedAt == null).ToList());
    public Task<CardRelationship?> FindActiveAsync(Guid sourceCardId, Guid targetCardId, RelationshipType type, CancellationToken ct = default)
        => Task.FromResult<CardRelationship?>(_relationships.FirstOrDefault(r => r.SourceCardId == sourceCardId && r.TargetCardId == targetCardId && r.Type == type && r.ArchivedAt == null));
    public Task AddAsync(CardRelationship relationship, CancellationToken ct = default) { _relationships.Add(relationship); return Task.CompletedTask; }
    public Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var rel = _relationships.FirstOrDefault(r => r.Id == id);
        if (rel != null) rel.ArchivedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task ArchiveRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        foreach (var rel in _relationships.Where(r => ids.Contains(r.Id)))
            rel.ArchivedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CardRelationship>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => r.ArchivedAt == null).ToList());

    public Task<IReadOnlyList<CardRelationship>> ListActiveByProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>(_relationships.Where(r => r.ArchivedAt == null).ToList());
}
