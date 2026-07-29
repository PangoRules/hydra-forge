namespace HydraForge.Server.Tests.Projects;

using System.Net;
using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Checklist;
using HydraForge.Application.Comments;
using HydraForge.Application.Notifications;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class ChecklistCommentsControllerTests
{
    [Fact]
    public async Task CreateChecklistItem_NonMember_Returns403()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(Guid.NewGuid(), "user", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/{projectId}/cards/{cardId}/cardchecklist"
        )
        {
            Content = new StringContent(
                """
                {"text": "Do this"}
                """,
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateChecklistItem_Member_Returns201()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "member", "m@m.com"));

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/{projectId}/cards/{cardId}/cardchecklist"
        )
        {
            Content = new StringContent(
                """
                {"text": "Do this"}
                """,
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Do this", body);
        Assert.Contains("\"isCompleted\":false", body);
    }

    [Fact]
    public async Task CreateChecklistItem_WithAssignee_ReturnsAssigneeInfo()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = assigneeId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "member", "m@m.com"));
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(assigneeId, "assignee", "a@a.com"));

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/{projectId}/cards/{cardId}/cardchecklist"
        )
        {
            Content = new StringContent(
                $"{{\"text\": \"Task\", \"assignedTo\": \"{assigneeId}\"}}",
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("assignee", body);
    }

    [Fact]
    public async Task CreateChecklistItem_InvalidAssignee_Returns400()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "member", "m@m.com"));
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(nonMemberId, "outsider", "o@o.com"));

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/{projectId}/cards/{cardId}/cardchecklist"
        )
        {
            Content = new StringContent(
                $"{{\"text\": \"Task\", \"assignedTo\": \"{nonMemberId}\"}}",
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CHECKLIST_INVALID_ASSIGNEE", body);
    }

    [Fact]
    public async Task ToggleChecklistItem_Member_ReturnsToggled()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "member", "m@m.com"));
        factory.AddChecklistItem(
            new ChecklistItem
            {
                Id = itemId,
                CardId = cardId,
                Text = "Task",
                Position = 0,
                IsCompleted = false,
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/projects/{projectId}/cards/{cardId}/cardchecklist/{itemId}/toggle"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"isCompleted\":true", body);
    }

    [Fact]
    public async Task ReorderChecklistItem_Member_ReturnsReordered()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "member", "m@m.com"));
        factory.AddChecklistItem(
            new ChecklistItem
            {
                Id = itemId,
                CardId = cardId,
                Text = "A",
                Position = 0,
            }
        );
        factory.AddChecklistItem(
            new ChecklistItem
            {
                Id = Guid.NewGuid(),
                CardId = cardId,
                Text = "B",
                Position = 1,
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/projects/{projectId}/cards/{cardId}/cardchecklist/{itemId}/reorder"
        )
        {
            Content = new StringContent(
                """
                {"newPosition": 1}
                """,
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"position\":1", body);
    }

    [Fact]
    public async Task DeleteChecklistItem_Member_Returns204()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "member", "m@m.com"));
        factory.AddChecklistItem(
            new ChecklistItem
            {
                Id = itemId,
                CardId = cardId,
                Text = "ToDelete",
                Position = 0,
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/projects/{projectId}/cards/{cardId}/cardchecklist/{itemId}"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_Member_ReturnsCommentWithMentionedUserIds()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var mentionedId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = mentionedId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "author", "a@a.com"));
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(mentionedId, "alice", "alice@a.com"));

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/{projectId}/cards/{cardId}/cardcomments"
        )
        {
            Content = new StringContent(
                """
                {"content": "Hey @alice check this"}
                """,
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hey @alice check this", body);
        Assert.Contains(mentionedId.ToString(), body);
    }

    [Fact]
    public async Task CreateComment_NonMember_Returns403()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(Guid.NewGuid(), "outsider", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/{projectId}/cards/{cardId}/cardcomments"
        )
        {
            Content = new StringContent(
                """
                {"content": "Hello"}
                """,
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_Member_ReturnsUpdated()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var commentId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "author", "a@a.com"));
        factory.AddComment(
            new Comment
            {
                Id = commentId,
                CardId = cardId,
                AuthorId = userId,
                Content = "Original",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/projects/{projectId}/cards/{cardId}/cardcomments/{commentId}"
        )
        {
            Content = new StringContent(
                """
                {"content": "Updated content"}
                """,
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Updated content", body);
    }

    [Fact]
    public async Task ArchiveComment_Member_ReturnsArchived()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var commentId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "author", "a@a.com"));
        factory.AddComment(
            new Comment
            {
                Id = commentId,
                CardId = cardId,
                AuthorId = userId,
                Content = "To archive",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/projects/{projectId}/cards/{cardId}/cardcomments/{commentId}"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"archivedAt\":", body);
    }

    [Fact]
    public async Task ListComments_Member_ReturnsComments()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "author", "a@a.com"));
        factory.AddComment(
            new Comment
            {
                Id = Guid.NewGuid(),
                CardId = cardId,
                AuthorId = userId,
                Content = "First comment",
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}/cards/{cardId}/cardcomments"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("First comment", body);
    }

    [Fact]
    public async Task ListChecklistItems_Member_ReturnsItems()
    {
        var factory = new ChecklistCommentsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = ChecklistCommentsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        factory.AddProject(new Project { Id = projectId, Name = "Test" });
        factory.AddMember(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddCard(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                ColumnId = Guid.NewGuid(),
                CardNumber = 1,
                Title = "Card",
            }
        );
        factory.AddUser(ChecklistCommentsTestWebApplicationFactory.MakeUser(userId, "member", "m@m.com"));
        factory.AddChecklistItem(
            new ChecklistItem
            {
                Id = Guid.NewGuid(),
                CardId = cardId,
                Text = "Task 1",
                Position = 0,
            }
        );

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}/cards/{cardId}/cardchecklist"
        );
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Task 1", body);
    }
}

internal class ChecklistCommentsTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly List<Project> _projects = [];
    private readonly List<ProjectMember> _members = [];
    private readonly List<Card> _cards = [];
    private readonly List<User> _users = [];
    private readonly List<ChecklistItem> _checklistItems = [];
    private readonly List<Comment> _comments = [];
    private readonly List<CardWatcher> _watchers = [];
    private readonly List<CardAssignee> _assignees = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting(
            "Jwt:SigningKey",
            "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
        );
        builder.ConfigureServices(services =>
        {
            foreach (
                var descriptor in services
                    .Where(d =>
                        d.ServiceType == typeof(ProjectService)
                        || d.ServiceType == typeof(Application.Columns.ColumnService)
                        || d.ServiceType == typeof(CardService)
                        || d.ServiceType == typeof(ChecklistService)
                        || d.ServiceType == typeof(CommentService)
                        || d.ServiceType == typeof(IProjectRepository)
                        || d.ServiceType == typeof(IColumnRepository)
                        || d.ServiceType == typeof(ICardRepository)
                        || d.ServiceType == typeof(ICardAssigneeRepository)
                        || d.ServiceType == typeof(ICardWatcherRepository)
                        || d.ServiceType == typeof(ICardRelationshipRepository)
                        || d.ServiceType == typeof(IProjectMemberRepository)
                        || d.ServiceType == typeof(IUserRepository)
                        || d.ServiceType == typeof(IProjectContextSnapshotRepository)
                        || d.ServiceType == typeof(IChatArchiveService)
                        || d.ServiceType
                            == typeof(Application.ProjectSnapshots.IProjectSnapshotRefresher)
                        || d.ServiceType == typeof(IChecklistItemRepository)
                        || d.ServiceType == typeof(ICommentRepository)
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IProjectRepository>(_ => new CCTestProjectRepository(_projects));
            services.AddScoped<IColumnRepository>(_ => new CCTestColumnRepository());
            services.AddScoped<ICardRepository>(_ => new CCTestCardRepository(_cards));
            services.AddScoped<ICardAssigneeRepository>(_ => new CCTestCardAssigneeRepository(
                _assignees
            ));
            services.AddScoped<ICardWatcherRepository>(_ => new CCTestCardWatcherRepository(
                _watchers
            ));
            services.AddScoped<ICardRelationshipRepository>(
                _ => new CCTestCardRelationshipRepository()
            );
            services.AddScoped<IProjectMemberRepository>(_ => new CCTestProjectMemberRepository(
                _members
            ));
            services.AddScoped<IUserRepository>(_ => new CCTestUserRepository(_users));
            services.AddScoped<IProjectContextSnapshotRepository>(
                _ => new CCTestSnapshotRepository()
            );
            services.AddScoped<IChatArchiveService>(_ => new CCTestChatArchiveService());
            services.AddScoped<IAuditLogWriter>(_ => new CCTestAuditLogWriter());
            services.AddScoped<Application.ProjectSnapshots.IProjectSnapshotRefresher>(
                _ => new TestSnapshotRefresher()
            );
            services.AddScoped<Application.Realtime.IProjectBoardEventPublisher>(
                _ => new FakeProjectBoardEventPublisher()
            );
            services.AddScoped<INotificationService>(_ => new FakeNotificationService());
            services.AddScoped<IChecklistItemRepository>(_ => new CCTestChecklistItemRepository(
                _checklistItems
            ));
            services.AddScoped<ICommentRepository>(_ => new CCTestCommentRepository(_comments));
            services.AddScoped<ProjectService>();
            services.AddScoped<Application.Columns.ColumnService>();
            services.AddScoped<CardService>();
            services.AddScoped<ProjectMemberService>();
            services.AddScoped<ChecklistService>();
            services.AddScoped<CommentService>();
        });
    }

    public void AddProject(Project project) => _projects.Add(project);

    public void AddMember(ProjectMember member) => _members.Add(member);

    public void AddCard(Card card) => _cards.Add(card);

    public void AddUser(User user) => _users.Add(user);

    public static User MakeUser(Guid id, string username, string email) =>
        User.Create(username, username, username, email, "hash", id: id);

    public void AddChecklistItem(ChecklistItem item) => _checklistItems.Add(item);

    public void AddComment(Comment comment) => _comments.Add(comment);

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
        _ = new System.Security.Claims.ClaimsPrincipal(identity);
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

internal class CCTestProjectRepository(List<Project> projects) : IProjectRepository
{
    private readonly List<Project> _projects = projects;

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _projects.Add(project);
        return Task.CompletedTask;
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_projects.FirstOrDefault(p => p.Id == id));

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
    )
    {
        var filtered = _projects.AsEnumerable();
        if (!includeArchived)
            filtered = filtered.Where(p => p.ArchivedAt == null);
        var all = filtered.ToList();
        var page = all.Skip(skip).Take(take).ToList();
        return Task.FromResult(new ProjectListPage(page, all.Count));
    }

    public Task<ProjectListPage> ListAllAsync(
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        var filtered = _projects.AsEnumerable();
        if (!includeArchived)
            filtered = filtered.Where(p => p.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (p.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            );
        IEnumerable<Project> sorted = sortBy switch
        {
            ProjectSortField.Name => sortDescending
                ? filtered.OrderByDescending(p => p.Name)
                : filtered.OrderBy(p => p.Name),
            ProjectSortField.UpdatedAt => sortDescending
                ? filtered.OrderByDescending(p => p.UpdatedAt)
                : filtered.OrderBy(p => p.UpdatedAt),
            _ => sortDescending
                ? filtered.OrderByDescending(p => p.CreatedAt)
                : filtered.OrderBy(p => p.CreatedAt),
        };
        var all = sorted.ToList();
        var page = all.Skip(skip).Take(take).ToList();
        return Task.FromResult(new ProjectListPage(page, all.Count));
    }

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var idx = _projects.FindIndex(p => p.Id == project.Id);
        if (idx >= 0)
            _projects[idx] = project;
        return Task.CompletedTask;
    }

    public Task<ProjectListPage> ListNonMemberProjectsAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken ct = default
    ) => throw new NotImplementedException();
}

internal class CCTestColumnRepository : IColumnRepository
{
    public Task AddAsync(Column column, CancellationToken ct = default) => Task.CompletedTask;

    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<Column?>(null);

    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Column>>([]);

    public Task UpdateAsync(Column column, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;

    public Task ReorderAsync(
        Guid projectId,
        IReadOnlyList<Guid> orderedColumnIds,
        CancellationToken ct = default
    ) => Task.CompletedTask;

    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal class CCTestCardRepository(List<Card> cards) : ICardRepository
{
    private readonly List<Card> _cards = cards;

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default) =>
        Task.FromResult(_cards.FirstOrDefault(c => c.Id == cardId));

    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Card>>(
            _cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id)
        );

    public Task<Card?> GetByProjectAndNumberAsync(
        Guid projectId,
        int cardNumber,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            _cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber)
        );

    public Task<IReadOnlyList<Card>> ListByProjectAsync(
        Guid projectId,
        CardListFilter filter,
        CancellationToken ct = default
    )
    {
        var query = _cards.Where(c => c.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(c =>
                c.Title.Contains(filter.Search, StringComparison.InvariantCultureIgnoreCase)
            );
        return Task.FromResult<IReadOnlyList<Card>>([.. query.OrderBy(c => c.Position)]);
    }

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult(
            _cards
                .Where(c => c.ProjectId == projectId)
                .Select(c => c.CardNumber)
                .DefaultIfEmpty(0)
                .Max()
        );

    public Task AddAsync(Card card, CancellationToken ct = default)
    {
        _cards.Add(card);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = _cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0)
            _cards[idx] = card;
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        foreach (var c in cards)
        {
            var idx = _cards.FindIndex(x => x.Id == c.Id);
            if (idx >= 0)
                _cards[idx] = c;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid cardId, CancellationToken ct = default)
    {
        _cards.RemoveAll(c => c.Id == cardId);
        return Task.CompletedTask;
    }

    public Task CompactColumnPositionsAsync(
        Guid columnId,
        int exceptPosition,
        CancellationToken ct = default
    ) => Task.CompletedTask;

    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default) =>
        Task.FromResult(_cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
}

internal class CCTestCardAssigneeRepository(List<CardAssignee> assignees) : ICardAssigneeRepository
{
    private readonly List<CardAssignee> _assignees = assignees;

    public Task<CardAssignee?> GetByCardAndUserAsync(
        Guid cardId,
        Guid userId,
        CancellationToken ct = default
    ) => Task.FromResult(_assignees.FirstOrDefault(a => a.CardId == cardId && a.UserId == userId));

    public Task<ILookup<Guid, CardAssignee>> ListByCardIdsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    ) => Task.FromResult(_assignees.Where(a => cardIds.Contains(a.CardId)).ToLookup(a => a.CardId));

    public Task<IReadOnlyList<CardAssignee>> ListByCardAsync(
        Guid cardId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<CardAssignee>>([
            .. _assignees.Where(a => a.CardId == cardId),
        ]);

    public Task AddAsync(CardAssignee assignee, CancellationToken ct = default)
    {
        _assignees.Add(assignee);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IReadOnlyList<CardAssignee> assignees, CancellationToken ct = default)
    {
        _assignees.AddRange(assignees);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        _assignees.RemoveAll(a => a.CardId == cardId && a.UserId == userId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CardAssignee>> ListByUserAsync(Guid userId) =>
        Task.FromResult<IReadOnlyList<CardAssignee>>([
            .. _assignees.Where(a => a.UserId == userId),
        ]);
}

internal class CCTestCardWatcherRepository(List<CardWatcher> watchers) : ICardWatcherRepository
{
    private readonly List<CardWatcher> _watchers = watchers;

    public Task<CardWatcher?> GetByCardAndUserAsync(
        Guid cardId,
        Guid userId,
        CancellationToken ct = default
    ) => Task.FromResult(_watchers.FirstOrDefault(w => w.CardId == cardId && w.UserId == userId));

    public Task<ILookup<Guid, CardWatcher>> ListByCardIdsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    ) => Task.FromResult(_watchers.Where(w => cardIds.Contains(w.CardId)).ToLookup(w => w.CardId));

    public Task<IReadOnlyList<CardWatcher>> ListByCardAsync(
        Guid cardId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<CardWatcher>>([.. _watchers.Where(w => w.CardId == cardId)]);

    public Task AddAsync(CardWatcher watcher, CancellationToken ct = default)
    {
        _watchers.Add(watcher);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IReadOnlyList<CardWatcher> watchers, CancellationToken ct = default)
    {
        _watchers.AddRange(watchers);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        _watchers.RemoveAll(w => w.CardId == cardId && w.UserId == userId);
        return Task.CompletedTask;
    }
}

internal class CCTestCardRelationshipRepository : ICardRelationshipRepository
{
    public Task<IReadOnlyList<CardRelationship>> ListByCardAsync(
        Guid cardId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);

    public Task<IReadOnlyList<CardRelationship>> ListBlockersForCardAsync(
        Guid cardId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);

    public Task<IReadOnlyList<CardRelationship>> ListPredecessorsAsync(
        Guid cardId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);

    public Task<IReadOnlyList<CardRelationship>> ListBlockersForCardsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);

    public Task<CardRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<CardRelationship?>(null);

    public Task<IReadOnlyList<CardRelationship>> ListActiveByCardAsync(
        Guid cardId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);

    public Task<CardRelationship?> FindActiveAsync(
        Guid sourceCardId,
        Guid targetCardId,
        RelationshipType type,
        CancellationToken ct = default
    ) => Task.FromResult<CardRelationship?>(null);

    public Task AddAsync(CardRelationship relationship, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ArchiveAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;

    public Task ArchiveRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<CardRelationship>> ListByProjectAsync(
        Guid projectId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);

    public Task<IReadOnlyList<CardRelationship>> ListActiveByProjectAsync(
        Guid projectId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<CardRelationship>>([]);
}

internal class CCTestProjectMemberRepository(List<ProjectMember> members) : IProjectMemberRepository
{
    private readonly List<ProjectMember> _members = members;

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_members.FirstOrDefault(m => m.Id == id));

    public Task<ProjectMember?> GetByProjectAndUserAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            _members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId)
        );

    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
        Guid projectId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<ProjectMember>>([
            .. _members.Where(m => m.ProjectId == projectId),
        ]);

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        var counts = _members
            .Where(m => idList.Contains(m.ProjectId))
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
        var roles = _members
            .Where(m => idList.Contains(m.ProjectId) && m.UserId == userId)
            .ToDictionary(m => m.ProjectId, m => m.Role);
        return Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(roles);
    }

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        _members.Add(member);
        return Task.CompletedTask;
    }

    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = _members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0)
            _members[idx] = member;
        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
    {
        _members.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }
}

internal class CCTestUserRepository(List<User> users) : IUserRepository
{
    private readonly List<User> _users = users;

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, User>>(
            _users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id)
        );

    public Task<User?> FindByUsernameAsync(string username) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Username == username));

    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
        IReadOnlyList<string> usernames,
        string? searchTerm = null,
        int maxResults = 10,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<string, User>>(
            _users
                .Where(u => usernames.Contains(u.Username, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase)
        );

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task CreateAsync(User user, CancellationToken ct = default)
    {
        _users.Add(user);
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

internal class CCTestSnapshotRepository : IProjectContextSnapshotRepository
{
    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken ct = default
    ) => Task.FromResult<ProjectContextSnapshot?>(null);

    public Task UpdateAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal class CCTestChatArchiveService : IChatArchiveService
{
    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal class CCTestAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default) =>
        Task.FromResult(Result.Success());
}

internal class CCTestChecklistItemRepository(List<ChecklistItem> items) : IChecklistItemRepository
{
    private readonly List<ChecklistItem> _items = items;

    public Task<ChecklistItem?> GetByIdAsync(Guid itemId, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(i => i.Id == itemId));

    public Task<IReadOnlyList<ChecklistItem>> ListByCardAsync(
        Guid cardId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<ChecklistItem>>([
            .. _items.Where(i => i.CardId == cardId).OrderBy(i => i.Position),
        ]);

    public Task<int> GetMaxPositionAsync(Guid cardId, CancellationToken ct = default) =>
        Task.FromResult(
            _items.Where(i => i.CardId == cardId).Select(i => i.Position).DefaultIfEmpty(-1).Max()
        );

    public Task AddAsync(ChecklistItem item, CancellationToken ct = default)
    {
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ChecklistItem item, CancellationToken ct = default)
    {
        var idx = _items.FindIndex(i => i.Id == item.Id);
        if (idx >= 0)
            _items[idx] = item;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid itemId, CancellationToken ct = default)
    {
        _items.RemoveAll(i => i.Id == itemId);
        return Task.CompletedTask;
    }

    public Task CompactPositionsAsync(
        Guid cardId,
        int deletedPosition,
        CancellationToken ct = default
    )
    {
        var toShift = _items
            .Where(i => i.CardId == cardId && i.Position > deletedPosition)
            .ToList();
        foreach (var item in toShift)
            item.Position -= 1;
        return Task.CompletedTask;
    }

    public Task UpdatePositionsAsync(
        IReadOnlyList<ChecklistItem> items,
        CancellationToken ct = default
    )
    {
        foreach (var item in items)
        {
            var idx = _items.FindIndex(i => i.Id == item.Id);
            if (idx >= 0)
                _items[idx] = item;
        }
        return Task.CompletedTask;
    }
}

internal class CCTestCommentRepository(List<Comment> comments) : ICommentRepository
{
    private readonly List<Comment> _comments = comments;

    public Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken ct = default) =>
        Task.FromResult(_comments.FirstOrDefault(c => c.Id == commentId));

    public Task<IReadOnlyList<Comment>> ListByCardAsync(
        Guid cardId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<Comment>>([
            .. _comments.Where(c => c.CardId == cardId).OrderBy(c => c.CreatedAt),
        ]);

    public Task AddAsync(Comment comment, CancellationToken ct = default)
    {
        _comments.Add(comment);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Comment comment, CancellationToken ct = default)
    {
        var idx = _comments.FindIndex(c => c.Id == comment.Id);
        if (idx >= 0)
            _comments[idx] = comment;
        return Task.CompletedTask;
    }
}
