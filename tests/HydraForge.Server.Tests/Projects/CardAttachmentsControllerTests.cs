namespace HydraForge.Server.Tests.Projects;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HydraForge.Application.Audit;
using HydraForge.Application.Attachments;
using HydraForge.Application.Cards;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Result = HydraForge.Domain.Common.Result;

public class CardAttachmentsControllerTests
{
    [Fact]
    public async Task Upload_ValidPng_ReturnsCreated()
    {
        var factory = new AttachmentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Test Column" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Card" });

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardId}/attachments")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_NonMember_Returns403()
    {
        var factory = new AttachmentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "user", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardId}/attachments")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedContentType_Returns415()
    {
        var factory = new AttachmentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Test Column" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Card" });

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-executable");
        content.Add(fileContent, "file", "test.exe");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardId}/attachments")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task List_ValidMember_ReturnsAttachments()
    {
        var factory = new AttachmentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Test Column" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Card" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardId}/attachments");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Download_ValidAttachment_ReturnsFile()
    {
        var factory = new AttachmentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Test Column" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Card" });
        factory.AddAttachment(new Attachment
        {
            Id = attachmentId,
            CardId = cardId,
            FileName = "test.png",
            ContentType = "image/png",
            Size = 3,
            StoragePath = "some/key"
        });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardId}/attachments/{attachmentId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Download_NotFound_Returns404()
    {
        var factory = new AttachmentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Test Column" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Card" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/cards/{cardId}/attachments/{Guid.NewGuid()}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ValidAttachment_Returns204()
    {
        var factory = new AttachmentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        factory.AddColumn(new Column { Id = columnId, ProjectId = projectId, Name = "Test Column" });
        factory.AddCard(new Card { Id = cardId, ProjectId = projectId, ColumnId = columnId, CardNumber = 1, Title = "Card" });
        factory.AddAttachment(new Attachment
        {
            Id = attachmentId,
            CardId = cardId,
            FileName = "test.png",
            ContentType = "image/png",
            Size = 3,
            StoragePath = "some/key"
        });

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}/cards/{cardId}/attachments/{attachmentId}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

internal class AttachmentsTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];
    private readonly List<Column> _columns = [];
    private readonly List<Card> _cards = [];
    private readonly List<User> _users = [];
    private readonly List<Attachment> _attachments = [];
    private readonly FakeFileStore _fakeFileStore = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Jwt:SigningKey", "test-secret-key-that-is-at-least-32-chars-long-for-hs256");
        builder.ConfigureServices(services =>
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

            foreach (var descriptor in services.Where(d =>
                d.ServiceType == typeof(HydraForge.Application.Projects.ProjectService)
                || d.ServiceType == typeof(HydraForge.Application.Columns.ColumnService)
                || d.ServiceType == typeof(HydraForge.Application.Cards.CardService)
                || d.ServiceType == typeof(HydraForge.Application.Attachments.AttachmentService)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IProjectRepository)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IColumnRepository)
                || d.ServiceType == typeof(HydraForge.Application.Cards.ICardRepository)
                || d.ServiceType == typeof(HydraForge.Application.Cards.ICardAssigneeRepository)
                || d.ServiceType == typeof(HydraForge.Application.Cards.ICardWatcherRepository)
                || d.ServiceType == typeof(HydraForge.Application.Cards.ICardRelationshipRepository)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IProjectMemberRepository)
                || d.ServiceType == typeof(HydraForge.Application.Auth.IUserRepository)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IProjectContextSnapshotRepository)
                || d.ServiceType == typeof(HydraForge.Application.Projects.IChatArchiveService)
                || d.ServiceType == typeof(HydraForge.Application.Attachments.IAttachmentRepository)
                || d.ServiceType == typeof(HydraForge.Application.Attachments.IFileStore)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddScoped<HydraForge.Application.Projects.IProjectRepository>(_ => new AttachmentsTestProjectRepository(_projects));
            services.AddScoped<HydraForge.Application.Projects.IColumnRepository>(_ => new AttachmentsTestColumnRepository(_columns));
            services.AddScoped<HydraForge.Application.Cards.ICardRepository>(_ => new AttachmentsTestCardRepository(_cards));
            services.AddScoped<HydraForge.Application.Cards.ICardAssigneeRepository>(_ => new AttachmentsTestCardAssigneeRepository());
            services.AddScoped<HydraForge.Application.Cards.ICardWatcherRepository>(_ => new AttachmentsTestCardWatcherRepository());
            services.AddScoped<HydraForge.Application.Cards.ICardRelationshipRepository>(_ => new AttachmentsTestCardRelationshipRepository());
            services.AddScoped<HydraForge.Application.Projects.IProjectMemberRepository>(_ => new AttachmentsTestProjectMemberRepository(_members));
            services.AddScoped<HydraForge.Application.Auth.IUserRepository>(_ => new AttachmentsTestUserRepository(_users));
            services.AddScoped<HydraForge.Application.Projects.IProjectContextSnapshotRepository>(_ => new AttachmentsTestSnapshotRepository());
            services.AddScoped<HydraForge.Application.Projects.IChatArchiveService>(_ => new AttachmentsTestChatArchiveService());
            services.AddScoped<IAuditLogWriter>(_ => new AttachmentsTestAuditLogWriter());
            services.AddScoped<HydraForge.Application.Attachments.IFileStore>(_ => _fakeFileStore);
            services.AddScoped<HydraForge.Application.Attachments.IAttachmentRepository>(_ => new AttachmentsTestAttachmentRepository(_attachments));
            services.AddScoped<HydraForge.Application.Projects.ProjectService>();
            services.AddScoped<HydraForge.Application.Columns.ColumnService>();
            services.AddScoped<HydraForge.Application.Cards.CardService>();
            services.AddScoped<HydraForge.Application.Projects.ProjectMemberService>();
            services.AddScoped(sp =>
            {
                var fileStore = sp.GetRequiredService<HydraForge.Application.Attachments.IFileStore>();
                var attachmentRepo = sp.GetRequiredService<HydraForge.Application.Attachments.IAttachmentRepository>();
                var cardRepo = sp.GetRequiredService<HydraForge.Application.Cards.ICardRepository>();
                var memberRepo = sp.GetRequiredService<HydraForge.Application.Projects.IProjectMemberRepository>();
                var auditWriter = sp.GetRequiredService<IAuditLogWriter>();
                return new HydraForge.Application.Attachments.AttachmentService(
                    attachmentRepo, cardRepo, memberRepo, fileStore, auditWriter,
                    10_000_000, HydraForge.Application.Attachments.AttachmentContentTypes.Allowed);
            });
        });
    }

    public void AddProject(Project project) => _projects.Add(project);
    public void AddMember(ProjectMember member) => _members.Add(member);
    public void AddColumn(Column column) => _columns.Add(column);
    public void AddCard(Card card) => _cards.Add(card);
    public void AddUser(User user) => _users.Add(user);
    public void AddAttachment(Attachment attachment) => _attachments.Add(attachment);

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

internal class FakeFileStore : HydraForge.Application.Attachments.IFileStore
{
    public Task<Result<string>> StoreAsync(Stream content, string contentType, string storageKey, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Success(storageKey));

    public Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult(Result<Stream>.Success(new MemoryStream([1, 2, 3])));

    public Task<Result> DeleteAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult(Result.Success());
}

internal class AttachmentsTestProjectRepository : HydraForge.Application.Projects.IProjectRepository
{
    private readonly List<Project> _projects;

    public AttachmentsTestProjectRepository(List<Project> projects) => _projects = projects;

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

internal class AttachmentsTestColumnRepository : HydraForge.Application.Projects.IColumnRepository
{
    private readonly List<Column> _columns;

    public AttachmentsTestColumnRepository(List<Column> columns) => _columns = columns;

    public Task AddAsync(Column column, CancellationToken ct = default) { _columns.Add(column); return Task.CompletedTask; }
    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<Column?>(_columns.FirstOrDefault(c => c.Id == id));
    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Column>>(_columns.Where(c => c.ProjectId == projectId).ToList());
    public Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        var idx = _columns.FindIndex(c => c.Id == column.Id);
        if (idx >= 0) _columns[idx] = column;
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _columns.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default) => Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default) { _columns.AddRange(columns); return Task.CompletedTask; }
}

internal class AttachmentsTestCardRepository : HydraForge.Application.Cards.ICardRepository
{
    private readonly List<Card> _cards;

    public AttachmentsTestCardRepository(List<Card> cards) => _cards = cards;

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(_cards.FirstOrDefault(c => c.Id == cardId));
    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(_cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));
    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult(_cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber));
    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, HydraForge.Application.Cards.CardListFilter filter, CancellationToken ct = default)
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
    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        foreach (var c in cards)
        {
            var idx = _cards.FindIndex(x => x.Id == c.Id);
            if (idx >= 0) _cards[idx] = c;
        }
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { _cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default) => Task.CompletedTask;
    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
        => Task.FromResult(_cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
}

internal class AttachmentsTestCardAssigneeRepository : HydraForge.Application.Cards.ICardAssigneeRepository
{
    public Task<CardAssignee?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<CardAssignee?>(null);
    public Task<ILookup<Guid, CardAssignee>> ListByCardIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<ILookup<Guid, CardAssignee>>(Array.Empty<CardAssignee>().ToLookup(a => a.CardId));
    public Task<IReadOnlyList<CardAssignee>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardAssignee>>([]);
    public Task AddAsync(CardAssignee assignee, CancellationToken ct = default) { return Task.CompletedTask; }
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { return Task.CompletedTask; }
}

internal class AttachmentsTestCardWatcherRepository : HydraForge.Application.Cards.ICardWatcherRepository
{
    public Task<CardWatcher?> GetByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<CardWatcher?>(null);
    public Task<ILookup<Guid, CardWatcher>> ListByCardIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<ILookup<Guid, CardWatcher>>(Array.Empty<CardWatcher>().ToLookup(w => w.CardId));
    public Task<IReadOnlyList<CardWatcher>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardWatcher>>([]);
    public Task AddAsync(CardWatcher watcher, CancellationToken ct = default) { return Task.CompletedTask; }
    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default) { return Task.CompletedTask; }
}

internal class AttachmentsTestCardRelationshipRepository : HydraForge.Application.Cards.ICardRelationshipRepository
{
    public Task<IReadOnlyList<CardRelationship>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>([]);
    public Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>([]);
    public Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>([]);
    public Task<CardRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<CardRelationship?>(null);
    public Task<IReadOnlyList<CardRelationship>> ListActiveByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CardRelationship>>([]);
    public Task<CardRelationship?> FindActiveAsync(Guid sourceCardId, Guid targetCardId, RelationshipType type, CancellationToken ct = default)
        => Task.FromResult<CardRelationship?>(null);
    public Task AddAsync(CardRelationship relationship, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task ArchiveAsync(Guid id, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task ArchiveRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Task.CompletedTask;
}

internal class AttachmentsTestProjectMemberRepository : HydraForge.Application.Projects.IProjectMemberRepository
{
    private readonly List<ProjectMember> _members;

    public AttachmentsTestProjectMemberRepository(List<ProjectMember> members) => _members = members;

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) { _members.Add(member); return Task.CompletedTask; }
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
    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { _members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = _members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0) _members[idx] = member;
        return Task.CompletedTask;
    }
}

internal class AttachmentsTestUserRepository : HydraForge.Application.Auth.IUserRepository
{
    private readonly List<User> _users;

    public AttachmentsTestUserRepository(List<User> users) => _users = users;

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

internal class AttachmentsTestSnapshotRepository : HydraForge.Application.Projects.IProjectContextSnapshotRepository
{
    public Task AddAsync(HydraForge.Domain.Entities.ProjectSpace.ProjectContextSnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
    public Task<HydraForge.Domain.Entities.ProjectSpace.ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<HydraForge.Domain.Entities.ProjectSpace.ProjectContextSnapshot?>(null);
}

internal class AttachmentsTestChatArchiveService : HydraForge.Application.Projects.IChatArchiveService
{
    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default) => Task.CompletedTask;
}

internal class AttachmentsTestAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
        => Task.FromResult(Result.Success());
}

internal class AttachmentsTestAttachmentRepository : HydraForge.Application.Attachments.IAttachmentRepository
{
    private readonly List<Attachment> _attachments;

    public AttachmentsTestAttachmentRepository(List<Attachment> attachments) => _attachments = attachments;

    public Task<Attachment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_attachments.FirstOrDefault(a => a.Id == id));
    public Task<IReadOnlyList<Attachment>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Attachment>>(_attachments.Where(a => a.CardId == cardId).ToList());
    public Task AddAsync(Attachment attachment, CancellationToken ct = default) { _attachments.Add(attachment); return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _attachments.RemoveAll(a => a.Id == id); return Task.CompletedTask; }
}
