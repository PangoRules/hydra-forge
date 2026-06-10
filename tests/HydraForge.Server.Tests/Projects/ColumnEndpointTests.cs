namespace HydraForge.Server.Tests.Projects;

using System.Net;
using System.Text;
using HydraForge.Application.Cards;
using HydraForge.Application.Columns;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class ColumnEndpointTests
{
    [Fact]
    public async Task List_NonMember_Returns403()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/columns");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task List_Member_ReturnsColumns()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddColumn(new Column { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Done", Position = 1 });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/columns");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Backlog", body);
        Assert.Contains("Done", body);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/columns")
        {
            Content = new StringContent(
                "{\"name\":\"In Progress\",\"color\":\"#FF0000\",\"wipLimit\":5}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("In Progress", body);
        Assert.Contains("0", body);
    }

    [Fact]
    public async Task Create_NonMember_Returns403()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/columns")
        {
            Content = new StringContent(
                "{\"name\":\"New Column\",\"color\":null,\"wipLimit\":null}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsUpdatedColumn()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}/columns/{columnId}")
        {
            Content = new StringContent(
                "{\"name\":\"In Progress\",\"color\":\"#00FF00\",\"wipLimit\":3}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("In Progress", body);
        Assert.Contains("#00FF00", body);
    }

    [Fact]
    public async Task Update_NonExistentColumn_Returns404()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}/columns/{Guid.NewGuid()}")
        {
            Content = new StringContent(
                "{\"name\":\"Updated\",\"color\":null,\"wipLimit\":null}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("COLUMN_NOT_FOUND", body);
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task Delete_EmptyColumn_Returns204()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "To Delete", Position = 0 });

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/columns/{columnId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonEmptyColumn_Returns409()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Has Cards", Position = 0 });
        factory.AddCard(new Card { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = columnId, Title = "Task 1" });

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/columns/{columnId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("COLUMN_DELETE_NON_EMPTY", body);
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task Reorder_ValidIds_Returns204()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var col1Id = Guid.NewGuid();
        var col2Id = Guid.NewGuid();
        var col3Id = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = col1Id, ProjectId = projectId, Name = "Backlog", Position = 0 });
        factory.AddColumn(new Column { Id = col2Id, ProjectId = projectId, Name = "In Dev", Position = 1 });
        factory.AddColumn(new Column { Id = col3Id, ProjectId = projectId, Name = "Done", Position = 2 });

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}/columns/reorder")
        {
            Content = new StringContent(
                $"{{\"columnIds\":[\"{col3Id}\",\"{col1Id}\",\"{col2Id}\"]}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_InvalidIds_Returns400()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var col1Id = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = col1Id, ProjectId = projectId, Name = "Backlog", Position = 0 });

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}/columns/reorder")
        {
            Content = new StringContent(
                $"{{\"columnIds\":[\"{col1Id}\",\"{Guid.NewGuid()}\"]}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("COLUMN_INVALID_POSITION", body);
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task GetById_ExistingColumn_ReturnsColumn()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);

        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0, Color = "#0000FF", WipLimit = 5 });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/columns/{columnId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Backlog", body);
        Assert.Contains("#0000FF", body);
        Assert.Contains("5", body);
    }

    [Fact]
    public async Task GetById_NonMember_Returns403()
    {
        var factory = new ColumnsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = factory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);

        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/columns/{columnId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

internal class ColumnsTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];
    private readonly List<Column> _columns = [];
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
                || d.ServiceType == typeof(ColumnService)
                || d.ServiceType == typeof(IProjectRepository)
                || d.ServiceType == typeof(IColumnRepository)
                || d.ServiceType == typeof(ICardRepository)
                || d.ServiceType == typeof(IProjectMemberRepository)
                || d.ServiceType == typeof(IProjectContextSnapshotRepository)
                || d.ServiceType == typeof(IChatArchiveService)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IProjectRepository>(_ => new ColTestProjectRepository(_projects));
            services.AddScoped<IColumnRepository>(_ => new ColTestColumnRepository(_columns));
            services.AddScoped<ICardRepository>(_ => new ColTestCardRepository(_cards));
            services.AddScoped<IProjectMemberRepository>(_ => new ColTestColumnMemberRepository(_members));
            services.AddScoped<IProjectContextSnapshotRepository>(_ => new ColTestColumnSnapshotRepository());
            services.AddScoped<IChatArchiveService>(_ => new ColTestColumnChatArchiveService());
            services.AddScoped<ProjectService>();
            services.AddScoped<ColumnService>();
            services.AddScoped<ProjectMemberService>();
        });
    }

    public void AddProject(Project project) => _projects.Add(project);
    public void AddMember(ProjectMember member) => _members.Add(member);
    public void AddColumn(Column column) => _columns.Add(column);
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

internal class ColTestProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects;

    public ColTestProjectRepository(List<Project> projects) => _projects = projects;

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

internal class ColTestColumnRepository : IColumnRepository
{
    private readonly List<Column> _columns;

    public ColTestColumnRepository(List<Column> columns) => _columns = columns;

    public Task AddAsync(Column column, CancellationToken ct = default)
    {
        _columns.Add(column);
        return Task.CompletedTask;
    }

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

    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default)
    {
        _columns.AddRange(columns);
        return Task.CompletedTask;
    }
}

internal class ColTestCardRepository : ICardRepository
{
    private readonly List<Card> _cards;

    public ColTestCardRepository(List<Card> cards) => _cards = cards;

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(_cards.FirstOrDefault(c => c.Id == cardId));

    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(_cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));

    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult(_cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber));

    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
    {
        var query = _cards.Where(c => c.ProjectId == projectId);
        if (filter.ColumnId.HasValue)
            query = query.Where(c => c.ColumnId == filter.ColumnId.Value);
        if (!filter.IncludeArchived)
            query = query.Where(c => c.ArchivedAt == null);
        if (filter.Type.HasValue)
            query = query.Where(c => c.Type == filter.Type.Value);
        return Task.FromResult<IReadOnlyList<Card>>(query.ToList());
    }

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult(_cards.Where(c => c.ProjectId == projectId).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());

    public Task AddAsync(Card card, CancellationToken ct = default)
    {
        _cards.Add(card);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = _cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0) _cards[idx] = card;
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default) { foreach (var c in cards) { var idx = _cards.FindIndex(x => x.Id == c.Id); if (idx >= 0) _cards[idx] = c; } return Task.CompletedTask; }

    public Task DeleteAsync(Guid cardId, CancellationToken ct = default)
    {
        _cards.RemoveAll(c => c.Id == cardId);
        return Task.CompletedTask;
    }

    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
        => Task.FromResult(_cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
}

internal class ColTestColumnMemberRepository : IProjectMemberRepository
{
    private readonly List<ProjectMember> _members;

    public ColTestColumnMemberRepository(List<ProjectMember> members) => _members = members;

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

internal class ColTestColumnSnapshotRepository : IProjectContextSnapshotRepository
{
    private readonly List<ProjectContextSnapshot> _snapshots = [];

    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        _snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<ProjectContextSnapshot?>(_snapshots.FirstOrDefault(s => s.ProjectId == projectId));
}

internal class ColTestColumnChatArchiveService : IChatArchiveService
{
    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.CompletedTask;
}