namespace HydraForge.Server.Tests.Projects;

using System.Net;
using System.Text;
using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class CardsControllerTests
{
    [Fact]
    public async Task List_NonMember_Returns403()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);
        var projectId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task List_Member_ReturnsCards()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Card 1" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Card 1", body);
    }

    [Fact]
    public async Task GetByIdOrNumber_Guid_ReturnsCard()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), CardNumber = 1, Title = "Card by Guid" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Card by Guid", body);
    }

    [Fact]
    public async Task GetByIdOrNumber_IntNumber_ReturnsCard()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddCard(new Card { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = Guid.NewGuid(), CardNumber = 42, Title = "Card 42" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/42");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Card 42", body);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards")
        {
            Content = new StringContent(
                $"{{\"columnId\":\"{columnId}\",\"title\":\"New Card\",\"description\":\"\",\"type\":\"Task\",\"parentCardId\":null,\"dueAt\":null}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("New Card", body);
    }

    [Fact]
    public async Task Move_ValidMove_ReturnsMovedCard()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Position = 0, Version = 1 });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardId}/move")
        {
            Content = new StringContent(
                $"{{\"targetColumnId\":\"{columnId}\",\"targetPosition\":1,\"confirmBlockedMove\":false,\"version\":1}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Assign_ValidRequest_ReturnsCardWithAssignee()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), CardNumber = 1, Title = "Card" });
        factory.AddUser(new User { Id = assigneeId, Username = "assignee" });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardId}/assignees")
        {
            Content = new StringContent(
                $"{{\"assigneeUserId\":\"{assigneeId}\"}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("assignee", body);
    }

    [Fact]
    public async Task Unassign_ValidRequest_ReturnsCardWithoutAssignee()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), CardNumber = 1, Title = "Card" });
        factory.AddCardAssignee(new CardAssignee { CardId = cardId, UserId = assigneeId, AssignedByUserId = userId });

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/cards/{cardId}/assignees/{assigneeId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByColumn()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var col1 = Guid.NewGuid();
        var col2 = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = col1, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddColumn(new Column { Id = col2, ProjectId = projectId, Name = "Done", Position = 1 });
        factory.AddCard(new Card { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = col1, CardNumber = 1, Title = "In Backlog" });
        factory.AddCard(new Card { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = col2, CardNumber = 2, Title = "In Done" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards?columnId={col1}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("In Backlog", body);
        Assert.DoesNotContain("In Done", body);
    }

    [Fact]
    public async Task List_IncludeArchived_ReturnsArchivedCards()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddCard(new Card { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Active" });
        factory.AddCard(new Card { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = columnId, CardNumber = 2, Title = "Archived", ArchivedAt = DateTime.UtcNow });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards?includeArchived=true");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Active", body);
        Assert.Contains("Archived", body);
    }

    [Fact]
    public async Task GetByIdOrNumber_NonMember_Returns403()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), CardNumber = 1, Title = "Card" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetByIdOrNumber_NotFound_Returns404()
    {
        var factory = new CardsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{Guid.NewGuid()}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CARD_NOT_FOUND", body);
        Assert.Contains("correlationId", body);
    }
}

internal class CardsTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];
    private readonly List<Column> _columns = [];
    private readonly List<Card> _cards = [];
    private readonly List<User> _users = [];
    private readonly List<CardAssignee> _cardAssignees = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Jwt:SigningKey", "test-secret-key-that-is-at-least-32-chars-long-for-hs256");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d =>
                d.ServiceType == typeof(HydraForge.Application.Projects.ProjectService)
                || d.ServiceType == typeof(HydraForge.Application.Columns.ColumnService)
                || d.ServiceType == typeof(HydraForge.Application.Cards.CardService)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IProjectRepository)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IColumnRepository)
                || d.ServiceType == typeof(HydraForge.Application.Cards.ICardRepository)
                || d.ServiceType == typeof(HydraForge.Application.Cards.ICardAssigneeRepository)
                || d.ServiceType == typeof(HydraForge.Application.Cards.ICardWatcherRepository)
                || d.ServiceType == typeof(HydraForge.Application.Cards.ICardRelationshipRepository)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IProjectMemberRepository)
                || d.ServiceType == typeof(HydraForge.Application.Auth.IUserRepository)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IProjectContextSnapshotRepository)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IChatArchiveService)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddScoped<HydraForge.Application.Projects.IProjectRepository>(_ => new CardsTestProjectRepository(_projects));
            services.AddScoped<HydraForge.Application.Projects.IColumnRepository>(_ => new CardsTestColumnRepository(_columns));
            services.AddScoped<HydraForge.Application.Cards.ICardRepository>(_ => new CardsTestCardRepository(_cards));
            services.AddScoped<HydraForge.Application.Cards.ICardAssigneeRepository>(_ => new CardsTestCardAssigneeRepository(_cardAssignees));
            services.AddScoped<HydraForge.Application.Cards.ICardWatcherRepository>(_ => new CardsTestCardWatcherRepository());
            services.AddScoped<HydraForge.Application.Cards.ICardRelationshipRepository>(_ => new CardsTestCardRelationshipRepository());
            services.AddScoped<HydraForge.Application.Projects.IProjectMemberRepository>(_ => new CardsTestProjectMemberRepository(_members));
            services.AddScoped<HydraForge.Application.Auth.IUserRepository>(_ => new CardsTestUserRepository(_users));
            services.AddScoped<HydraForge.Application.Projects.IProjectContextSnapshotRepository>(_ => new CardsTestSnapshotRepository());
            services.AddScoped<HydraForge.Application.Projects.IChatArchiveService>(_ => new CardsTestChatArchiveService());
            services.AddScoped<IAuditLogWriter>(_ => new CardsTestAuditLogWriter());
            services.AddScoped<HydraForge.Application.Projects.ProjectService>();
            services.AddScoped<HydraForge.Application.Columns.ColumnService>();
            services.AddScoped<HydraForge.Application.Cards.CardService>();
            services.AddScoped<HydraForge.Application.Projects.ProjectMemberService>();
            services.AddScoped<HydraForge.Application.Cards.CardRelationshipService>();
        });
    }

    public void AddProject(Project project) => _projects.Add(project);
    public void AddMember(ProjectMember member) => _members.Add(member);
    public void AddColumn(Column column) => _columns.Add(column);
    public void AddCard(Card card) => _cards.Add(card);
    public void AddUser(User user) => _users.Add(user);
    public void AddCardAssignee(CardAssignee assignee) => _cardAssignees.Add(assignee);

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

internal class CardsTestProjectRepository : HydraForge.Application.Projects.IProjectRepository
{
    private readonly List<Project> _projects;

    public CardsTestProjectRepository(List<Project> projects) => _projects = projects;

    public Task AddAsync(Project project, CancellationToken ct = default) { _projects.Add(project); return Task.CompletedTask; }
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

internal class CardsTestColumnRepository : HydraForge.Application.Projects.IColumnRepository
{
    private readonly List<Column> _columns;

    public CardsTestColumnRepository(List<Column> columns) => _columns = columns;

    public Task AddAsync(Column column, CancellationToken ct = default) { _columns.Add(column); return Task.CompletedTask; }
    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_columns.FirstOrDefault(c => c.Id == id));
    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Column>>(_columns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToList());
    public Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        var idx = _columns.FindIndex(c => c.Id == column.Id);
        if (idx >= 0) _columns[idx] = column;
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _columns.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default)
    {
        for (var i = 0; i < orderedColumnIds.Count; i++)
        {
            var col = _columns.FirstOrDefault(c => c.Id == orderedColumnIds[i]);
            if (col != null) col.Position = i;
        }
        return Task.CompletedTask;
    }
    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default) { _columns.AddRange(columns); return Task.CompletedTask; }
}

internal class CardsTestCardRepository : HydraForge.Application.Cards.ICardRepository
{
    private readonly List<Card> _cards;

    public CardsTestCardRepository(List<Card> cards) => _cards = cards;

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(_cards.FirstOrDefault(c => c.Id == cardId));
    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(_cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));
    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult(_cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber && c.ArchivedAt == null));
    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, HydraForge.Application.Cards.CardListFilter filter, CancellationToken ct = default)
    {
        var query = _cards.Where(c => c.ProjectId == projectId);
        if (filter.ColumnId.HasValue)
            query = query.Where(c => c.ColumnId == filter.ColumnId.Value);
        if (!filter.IncludeArchived)
            query = query.Where(c => c.ArchivedAt == null);
        if (filter.Type.HasValue)
            query = query.Where(c => c.Type == filter.Type.Value);
        return Task.FromResult<IReadOnlyList<Card>>(query.OrderBy(c => c.Position).ToList());
    }
    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult(_cards.Where(c => c.ProjectId == projectId && c.ArchivedAt == null).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());
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

internal class CardsTestCardAssigneeRepository : HydraForge.Application.Cards.ICardAssigneeRepository
{
    private readonly List<CardAssignee> _assignees;

    public CardsTestCardAssigneeRepository(List<CardAssignee> assignees) => _assignees = assignees;

    public Task<CardAssignee?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(_assignees.FirstOrDefault(a => a.CardId == cardId && a.UserId == userId));
    public Task<ILookup<Guid, CardAssignee>> ListByCardIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<ILookup<Guid, CardAssignee>>(_assignees.Where(a => cardIds.Contains(a.CardId)).ToLookup(a => a.CardId));
    public Task<IReadOnlyList<CardAssignee>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardAssignee>>(_assignees.Where(a => a.CardId == cardId).ToList());
    public Task AddAsync(CardAssignee assignee, CancellationToken ct = default) { _assignees.Add(assignee); return Task.CompletedTask; }
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { _assignees.RemoveAll(a => a.CardId == cardId && a.UserId == userId); return Task.CompletedTask; }
}

internal class CardsTestCardWatcherRepository : HydraForge.Application.Cards.ICardWatcherRepository
{
    private readonly List<CardWatcher> _watchers = [];

    public Task<CardWatcher?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(_watchers.FirstOrDefault(w => w.CardId == cardId && w.UserId == userId));
    public Task<ILookup<Guid, CardWatcher>> ListByCardIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<ILookup<Guid, CardWatcher>>(_watchers.Where(w => cardIds.Contains(w.CardId)).ToLookup(w => w.CardId));
    public Task<IReadOnlyList<CardWatcher>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardWatcher>>(_watchers.Where(w => w.CardId == cardId).ToList());
    public Task AddAsync(CardWatcher watcher, CancellationToken ct = default) { _watchers.Add(watcher); return Task.CompletedTask; }
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { _watchers.RemoveAll(w => w.CardId == cardId && w.UserId == userId); return Task.CompletedTask; }
}

internal class CardsTestCardRelationshipRepository : HydraForge.Application.Cards.ICardRelationshipRepository
{
    private readonly List<CardRelationship> _relationships = [];

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
}

internal class CardsTestProjectMemberRepository : HydraForge.Application.Projects.IProjectMemberRepository
{
    private readonly List<ProjectMember> _members;

    public CardsTestProjectMemberRepository(List<ProjectMember> members) => _members = members;

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) { _members.Add(member); return Task.CompletedTask; }
    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_members.FirstOrDefault(m => m.Id == id));
    public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<ProjectMember?>(_members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));
    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProjectMember>>(_members.Where(m => m.ProjectId == projectId).ToList());
    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
    {
        var idList = projectIds.ToList();
        var counts = _members.Where(m => idList.Contains(m.ProjectId)).GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }
    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { _members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = _members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0) _members[idx] = member;
        return Task.CompletedTask;
    }
}

internal class CardsTestUserRepository : HydraForge.Application.Auth.IUserRepository
{
    private readonly List<User> _users;

    public CardsTestUserRepository(List<User> users) => _users = users;

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, User>>(_users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id));
    public Task<User?> FindByUsernameAsync(string username)
        => Task.FromResult(_users.FirstOrDefault(u => u.Username == username));
    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, User>>(_users.Where(u => usernames.Contains(u.Username, StringComparer.OrdinalIgnoreCase)).ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase));
    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;
    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);
    public Task CreateAsync(User user) { _users.Add(user); return Task.CompletedTask; }
}

internal class CardsTestSnapshotRepository : HydraForge.Application.Projects.IProjectContextSnapshotRepository
{
    private readonly List<ProjectContextSnapshot> _snapshots = [];

    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default) { _snapshots.Add(snapshot); return Task.CompletedTask; }
    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<ProjectContextSnapshot?>(_snapshots.FirstOrDefault(s => s.ProjectId == projectId));
}

internal class CardsTestChatArchiveService : HydraForge.Application.Projects.IChatArchiveService
{
    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
}

internal class CardsTestAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
        => Task.FromResult(Result.Success());
}
