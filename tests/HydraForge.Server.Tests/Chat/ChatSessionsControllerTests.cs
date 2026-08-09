using System.Net;
using System.Text;
using HydraForge.Application.Chat;
using HydraForge.Application.Projects;
using HydraForge.Application.Settings;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Server.Tests.Chat;

public class ChatSessionsControllerLinkCardTests
{
    [Fact]
    public async Task LinkCard_Owner_ReturnsOk()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChatSessionsTestWebApplicationFactory.IssueToken(userId, "owner");
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddChatSession(
            new ChatSession
            {
                Id = sessionId,
                OwnerId = userId,
                ProjectId = projectId,
                Title = "Test Session",
                Status = ChatSessionStatus.Active,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Test Card",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/chat/sessions/{sessionId}/link-card"
        )
        {
            Content = new StringContent(
                $"{{\"cardId\":\"{cardId}\"}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkCard_SessionNotFound_ReturnsNotFound()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChatSessionsTestWebApplicationFactory.IssueToken(userId, "user");
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Test Card",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/chat/sessions/{Guid.NewGuid()}/link-card"
        )
        {
            Content = new StringContent(
                $"{{\"cardId\":\"{cardId}\"}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LinkCard_CardDifferentProject_ReturnsConflict()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChatSessionsTestWebApplicationFactory.IssueToken(userId, "owner");
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Project A" });
        factory.AddProject(new Project { Id = otherProjectId, Name = "Project B" });
        factory.AddChatSession(
            new ChatSession
            {
                Id = sessionId,
                OwnerId = userId,
                ProjectId = projectId,
                Title = "Test Session",
                Status = ChatSessionStatus.Active,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = otherProjectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card in Other Project",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/chat/sessions/{sessionId}/link-card"
        )
        {
            Content = new StringContent(
                $"{{\"cardId\":\"{cardId}\"}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LinkCard_NonMember_ReturnsForbidden()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var ownerId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var token = ChatSessionsTestWebApplicationFactory.IssueToken(nonMemberId, "stranger");
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test Project" });
        factory.AddChatSession(
            new ChatSession
            {
                Id = sessionId,
                OwnerId = ownerId,
                ProjectId = projectId,
                Title = "Test Session",
                Status = ChatSessionStatus.Active,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Test Card",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/chat/sessions/{sessionId}/link-card"
        )
        {
            Content = new StringContent(
                $"{{\"cardId\":\"{cardId}\"}}",
                Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LinkCard_NoAuth_Returns401()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var sessionId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/chat/sessions/{sessionId}/link-card"
        )
        {
            Content = new StringContent(
                $"{{\"cardId\":\"{cardId}\"}}",
                Encoding.UTF8,
                "application/json"
            ),
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

internal class ChatSessionsTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];
    private readonly List<ChatSession> _sessions = [];
    private readonly List<Card> _cards = [];
    private readonly List<User> _users = [];

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
                        d.ServiceType == typeof(ChatSessionService)
                        || d.ServiceType == typeof(IChatSessionService)
                        || d.ServiceType == typeof(IChatSessionRepository)
                        || d.ServiceType == typeof(IChatMessageRepository)
                        || d.ServiceType == typeof(IChatSessionDocumentRepository)
                        || d.ServiceType == typeof(Application.Cards.ICardRepository)
                        || d.ServiceType == typeof(Application.Auth.IUserRepository)
                        || d.ServiceType == typeof(IProjectMemberRepository)
                        || d.ServiceType == typeof(IProjectRepository)
                        || d.ServiceType == typeof(IAgentPersonalityRepository)
                        || d.ServiceType == typeof(IDocumentRepository)
                        || d.ServiceType == typeof(IChatSummaryGenerator)
                        || d.ServiceType == typeof(IBackgroundTaskQueue)
                        || d.ServiceType == typeof(ISettingsRepository)
                        || d.ServiceType == typeof(ISettingsProvider)
                        || d.ServiceType
                            == typeof(Microsoft.Extensions.Logging.ILogger<ChatSessionService>)
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IChatSessionRepository>(_ => new TestChatSessionRepository(
                _sessions
            ));
            services.AddScoped<IChatMessageRepository>(_ => new TestChatMessageRepository());
            services.AddScoped<IChatSessionDocumentRepository>(
                _ => new TestChatSessionDocumentRepository()
            );
            services.AddScoped<Application.Cards.ICardRepository>(_ => new TestCardRepository(
                _cards
            ));
            services.AddScoped<Application.Auth.IUserRepository>(_ => new TestUserRepository(
                _users
            ));
            services.AddScoped<IProjectMemberRepository>(_ => new TestProjectMemberRepository(
                _members
            ));
            services.AddScoped<IProjectRepository>(_ => new TestProjectRepository(_projects));
            services.AddScoped<IAgentPersonalityRepository>(
                _ => new TestAgentPersonalityRepository()
            );
            services.AddScoped<IDocumentRepository>(_ => new TestDocumentRepository());
            services.AddScoped<IChatSummaryGenerator>(_ => new TestChatSummaryGenerator());
            services.AddScoped<IBackgroundTaskQueue>(_ => new TestBackgroundTaskQueue());
            services.AddScoped<ISettingsRepository>(_ => new TestSettingsRepository());
            services.AddScoped<ISettingsProvider>(sp => new TestCachedSettingsProvider(
                sp.GetRequiredService<ISettingsRepository>()
            ));
            services.AddScoped<ChatSessionService>();
            services.AddScoped<IChatSessionService>(sp =>
                sp.GetRequiredService<ChatSessionService>()
            );
        });
    }

    public void AddProject(Project project) => _projects.Add(project);

    public void AddMember(ProjectMember member) => _members.Add(member);

    public void AddChatSession(ChatSession session) => _sessions.Add(session);

    public void AddCard(Card card) => _cards.Add(card);

    public void AddUser(User user) => _users.Add(user);

    public static string IssueToken(Guid userId, string username)
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                userId.ToString()
            ),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "User"),
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");

        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("test-secret-key-that-is-at-least-32-chars-long-for-hs256")
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

internal class TestChatSessionRepository(List<ChatSession> sessions) : IChatSessionRepository
{
    public Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default) =>
        Task.FromResult(sessions.FirstOrDefault(s => s.Id == sessionId));

    public Task<ChatSession?> GetActiveByPanelAsync(
        Guid projectId,
        Guid? openCardId,
        Guid ownerId,
        CancellationToken ct = default
    ) => Task.FromResult<ChatSession?>(null);

    public Task<IReadOnlyList<ChatSession>> ListAsync(
        Guid actorId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        bool isAdmin = false,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
        ChatSessionScope scope = ChatSessionScope.Mine,
        IReadOnlySet<ChatSessionKind>? types = null,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

    public Task<int> CountAsync(
        Guid actorId,
        Guid? folderId,
        Guid? projectId,
        bool isAdmin = false,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
        ChatSessionScope scope = ChatSessionScope.Mine,
        IReadOnlySet<ChatSessionKind>? types = null,
        CancellationToken ct = default
    ) => Task.FromResult(0);

    public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
        Guid actorId,
        string query,
        Guid? projectId,
        int limit,
        bool isAdmin = false,
        ChatSessionScope scope = ChatSessionScope.Mine,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

    public Task AddAsync(ChatSession session, CancellationToken ct = default)
    {
        sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ChatSession session, CancellationToken ct = default)
    {
        var idx = sessions.FindIndex(s => s.Id == session.Id);
        if (idx >= 0)
            sessions[idx] = session;
        return Task.CompletedTask;
    }

    public Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal class TestChatMessageRepository : IChatMessageRepository
{
    public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default) =>
        Task.FromResult<ChatMessage?>(null);

    public Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
        Guid sessionId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

    public Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
        Guid ownerId,
        string query,
        Guid? projectId,
        int limit,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

    public Task AddAsync(ChatMessage message, CancellationToken ct = default) => Task.CompletedTask;

    public Task<bool> DeleteFromAsync(
        Guid sessionId,
        Guid messageId,
        CancellationToken ct = default
    ) => Task.FromResult(false);
}

internal class TestChatSessionDocumentRepository : IChatSessionDocumentRepository
{
    public Task<IReadOnlyList<ChatSessionDocument>> GetBySessionAsync(
        Guid sessionId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<ChatSessionDocument>>([]);

    public Task AddAsync(ChatSessionDocument doc, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(Guid sessionId, Guid documentId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<bool> ExistsAsync(
        Guid sessionId,
        Guid documentId,
        CancellationToken ct = default
    ) => Task.FromResult(false);
}

internal class TestCardRepository(List<Card> cards) : Application.Cards.ICardRepository
{
    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default) =>
        Task.FromResult(cards.FirstOrDefault(c => c.Id == cardId));

    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Card>>(
            cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id)
        );

    public Task<Card?> GetByProjectAndNumberAsync(
        Guid projectId,
        int cardNumber,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            cards.FirstOrDefault(c =>
                c.ProjectId == projectId && c.CardNumber == cardNumber && c.ArchivedAt == null
            )
        );

    public Task<IReadOnlyList<Card>> ListByProjectAsync(
        Guid projectId,
        Application.Cards.CardListFilter filter,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Card>>([]);

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task AddAsync(Card card, CancellationToken ct = default)
    {
        cards.Add(card);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0)
            cards[idx] = card;
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(
        IReadOnlyList<Card> cardsToUpdate,
        CancellationToken ct = default
    ) => Task.CompletedTask;

    public Task DeleteAsync(Guid cardId, CancellationToken ct = default)
    {
        cards.RemoveAll(c => c.Id == cardId);
        return Task.CompletedTask;
    }

    public Task CompactColumnPositionsAsync(
        Guid columnId,
        int exceptPosition,
        CancellationToken ct = default
    ) => Task.CompletedTask;

    public Task<int> CountByColumnAsync(
        Guid columnId,
        bool includeArchived,
        CancellationToken ct = default
    ) => Task.FromResult(0);

    public Task<IReadOnlyList<Card>> GetByParentEpicIdAsync(
        Guid epicId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Card>>([]);

    public Task UpdateParentEpicAsync(
        Guid cardId,
        Guid? parentEpicId,
        CancellationToken ct = default
    ) => Task.CompletedTask;

    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task<int> CountActiveChildrenAsync(Guid parentCardId, CancellationToken ct = default) =>
        Task.FromResult(0);
}

internal class TestUserRepository(List<User> users) : Application.Auth.IUserRepository
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, User>>(
            users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id)
        );

    public Task<User?> FindByUsernameAsync(string username) =>
        Task.FromResult(users.FirstOrDefault(u => u.Username == username));

    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
        IReadOnlyList<string> usernames,
        string? searchTerm = null,
        int maxResults = 10,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<string, User>>(
            users
                .Where(u => usernames.Contains(u.Username))
                .ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase)
        );

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(users.Any(u => u.IsAdmin));

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(users.FirstOrDefault(u => u.Id == userId)?.IsAdmin ?? false);

    public Task CreateAsync(User user, CancellationToken ct = default)
    {
        users.Add(user);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> ListAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<User>>([]);

    public Task<int> CountAsync(string? search, CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
}

internal class TestProjectMemberRepository(List<ProjectMember> members) : IProjectMemberRepository
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
        Task.FromResult<IReadOnlyList<ProjectMember>>(
            members.Where(m => m.ProjectId == projectId).ToList()
        );

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyDictionary<Guid, int>>(projectIds.ToDictionary(id => id, _ => 0));

    public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
        IEnumerable<Guid> projectIds,
        Guid userId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(
            members
                .Where(m => m.UserId == userId && projectIds.Contains(m.ProjectId))
                .ToDictionary(m => m.ProjectId, m => m.Role)
        );

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        members.Add(member);
        return Task.CompletedTask;
    }

    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
}

internal class TestProjectRepository(List<Project> projects) : IProjectRepository
{
    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        projects.Add(project);
        return Task.CompletedTask;
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(projects.FirstOrDefault(p => p.Id == id));

    public Task<ProjectListPage> ListByUserIdAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        MemberRole? role,
        int skip,
        int take,
        CancellationToken ct = default
    ) => Task.FromResult(new ProjectListPage([], 0));

    public Task<ProjectListPage> ListAllAsync(
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken ct = default
    ) => Task.FromResult(new ProjectListPage([], 0));

    public Task UpdateAsync(Project project, CancellationToken ct = default) => Task.CompletedTask;

    public Task<ProjectListPage> ListNonMemberProjectsAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken ct = default
    ) => Task.FromResult(new ProjectListPage([], 0));
}

internal class TestAgentPersonalityRepository : IAgentPersonalityRepository
{
    public Task<AgentPersonality?> GetByIdAsync(
        Guid personalityId,
        CancellationToken ct = default
    ) => Task.FromResult<AgentPersonality?>(null);

    public Task<IReadOnlyList<AgentPersonality>> ListByUserAsync(
        Guid userId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<AgentPersonality>>([]);

    public Task<AgentPersonality?> GetDefaultAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<AgentPersonality?>(null);

    public Task AddAsync(AgentPersonality personality, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task UpdateAsync(AgentPersonality personality, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ArchiveAsync(Guid personalityId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetDefaultAsync(Guid personalityId, Guid userId, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal class TestDocumentRepository : IDocumentRepository
{
    public Task<Document?> GetByIdAsync(Guid documentId, CancellationToken ct = default) =>
        Task.FromResult<Document?>(null);

    public Task<IReadOnlyList<Document>> ListByUserAsync(
        Guid userId,
        string? q = null,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Document>>([]);

    public Task AddAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;

    public Task ArchiveAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
}

internal class TestChatSummaryGenerator : IChatSummaryGenerator
{
    public Task<Result<string>> GenerateSummaryAsync(
        Guid sessionId,
        CancellationToken ct = default
    ) => Task.FromResult(Result<string>.Success("Summary"));
}

internal class TestBackgroundTaskQueue : IBackgroundTaskQueue
{
    public Task EnqueueAsync(
        Func<CancellationToken, Task> workItem,
        CancellationToken ct = default
    ) => Task.CompletedTask;

    public Task EnqueueJobAsync<TJob>(
        System.Linq.Expressions.Expression<Func<TJob, Task>> methodCall
    ) => Task.CompletedTask;
}

internal class TestSettingsRepository : ISettingsRepository
{
    private SystemSettings _settings = new();

    public Task<SystemSettings> GetSingletonAsync(CancellationToken ct = default) =>
        Task.FromResult(_settings);

    public Task UpdateAsync(SystemSettings settings, CancellationToken ct = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}

internal class TestCachedSettingsProvider : ISettingsProvider
{
    private readonly ISettingsRepository _repo;

    public TestCachedSettingsProvider(ISettingsRepository repo) => _repo = repo;

    public Task<SystemSettings> GetAsync(CancellationToken ct = default) =>
        _repo.GetSingletonAsync(ct);

    public void Invalidate() { }
}
