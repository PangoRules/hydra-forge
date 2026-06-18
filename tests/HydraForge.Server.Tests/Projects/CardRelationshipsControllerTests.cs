namespace HydraForge.Server.Tests.Projects;

using System.Net;
using System.Text;
using System.Text.Json;
using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Domain.Common;
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
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddColumn(new Column { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), CardNumber = 1, Title = "Card" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardId}/relationships");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task List_Member_ReturnsRelationships()
    {
        var factory = new CardsTestWebApplicationFactory();
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

        var createRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createRequest.Headers.Add("Authorization", $"Bearer {token}");
        var createResponse = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardA}/relationships");
        listRequest.Headers.Add("Authorization", $"Bearer {token}");

        var listResponse = await client.SendAsync(listRequest);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains("Card B", body);
    }

    [Fact]
    public async Task Create_Duplicate_Returns409()
    {
        var factory = new CardsTestWebApplicationFactory();
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

        // Create first
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req1.Headers.Add("Authorization", $"Bearer {token}");
        var resp1 = await client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        // Create duplicate
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req2.Headers.Add("Authorization", $"Bearer {token}");
        var resp2 = await client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task Create_Cycle_Returns400()
    {
        var factory = new CardsTestWebApplicationFactory();
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

        // A blocks B
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req1.Headers.Add("Authorization", $"Bearer {token}");
        var resp1 = await client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        // B blocks A → cycle
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardB}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardA.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req2.Headers.Add("Authorization", $"Bearer {token}");
        var resp2 = await client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.BadRequest, resp2.StatusCode);
        var body = await resp2.Content.ReadAsStringAsync();
        Assert.Contains("cycle", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_CrossProject_Returns400()
    {
        var factory = new CardsTestWebApplicationFactory();
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

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Bearer {token}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("cross", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Relates_Success()
    {
        var factory = new CardsTestWebApplicationFactory();
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

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 3 }), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Bearer {token}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        var factory = new CardsTestWebApplicationFactory();
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

        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/cards/{cardId}/relationships/{Guid.NewGuid()}");
        req.Headers.Add("Authorization", $"Bearer {token}");

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_Success_Returns204()
    {
        var factory = new CardsTestWebApplicationFactory();
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
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createReq.Headers.Add("Authorization", $"Bearer {token}");
        var createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var createdBody = await createResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createdBody);
        var relId = doc.RootElement.GetProperty("id").GetString();

        // Delete
        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/cards/{cardA}/relationships/{relId}");
        deleteReq.Headers.Add("Authorization", $"Bearer {token}");
        var deleteResp = await client.SendAsync(deleteReq);

        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task ArchiveImpact_Preflight_Returns409WhenDependentsExist()
    {
        var factory = new CardsTestWebApplicationFactory();
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
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createReq.Headers.Add("Authorization", $"Bearer {token}");
        var createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        // Archive impact preflight on cardA (which blocks cardB)
        var impactReq = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardA}/archive-impact");
        impactReq.Headers.Add("Authorization", $"Bearer {token}");
        var impactResp = await client.SendAsync(impactReq);

        Assert.Equal(HttpStatusCode.Conflict, impactResp.StatusCode);
    }

    [Fact]
    public async Task ArchiveWithRelationships_Success()
    {
        var factory = new CardsTestWebApplicationFactory();
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
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { targetCardId = cardB.ToString(), type = 1 }), Encoding.UTF8, "application/json")
        };
        createReq.Headers.Add("Authorization", $"Bearer {token}");
        var createResp = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        // Archive with relationships
        var archiveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardA}/archive-with-relationships")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { confirm = true }), Encoding.UTF8, "application/json")
        };
        archiveReq.Headers.Add("Authorization", $"Bearer {token}");
        var archiveResp = await client.SendAsync(archiveReq);

        Assert.Equal(HttpStatusCode.OK, archiveResp.StatusCode);
    }
}

