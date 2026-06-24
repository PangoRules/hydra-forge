namespace HydraForge.Server.Tests.Realtime;

using System.Reflection;
using HydraForge.Application.Audit;
using HydraForge.Server.Tests.Projects;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

public class BoardHubIntegrationTests
{
    private class BoardHubIntegrationTestFactory : CardsTestWebApplicationFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
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
                    || d.ServiceType == typeof(HydraForge.Application.Projects.IChatArchiveService)
                    || d.ServiceType == typeof(HydraForge.Application.ProjectSnapshots.IProjectSnapshotRefresher)).ToList())
                {
                    services.Remove(descriptor);
                }

                var factoryType = typeof(CardsTestWebApplicationFactory);
                var projects = (System.Collections.Generic.List<HydraForge.Domain.Entities.ProjectSpace.Project>)
                    factoryType.GetField("_projects", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;
                var columns = (System.Collections.Generic.List<HydraForge.Domain.Entities.ProjectSpace.Column>)
                    factoryType.GetField("_columns", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;
                var cards = (System.Collections.Generic.List<HydraForge.Domain.Entities.ProjectSpace.Card>)
                    factoryType.GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;
                var members = (System.Collections.Generic.List<HydraForge.Domain.Entities.ProjectSpace.ProjectMember>)
                    factoryType.GetField("_members", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;
                var users = (System.Collections.Generic.List<HydraForge.Domain.Entities.Auth.User>)
                    factoryType.GetField("_users", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;
                var cardAssignees = (System.Collections.Generic.List<HydraForge.Domain.Entities.ProjectSpace.CardAssignee>)
                    factoryType.GetField("_cardAssignees", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;

                services.AddScoped<HydraForge.Application.Projects.IProjectRepository>(_ => new CardsTestProjectRepository(projects));
                services.AddScoped<HydraForge.Application.Projects.IColumnRepository>(_ => new CardsTestColumnRepository(columns));
                services.AddScoped<HydraForge.Application.Cards.ICardRepository>(_ => new CardsTestCardRepository(cards));
                services.AddScoped<HydraForge.Application.Cards.ICardAssigneeRepository>(_ => new CardsTestCardAssigneeRepository(cardAssignees));
                services.AddScoped<HydraForge.Application.Cards.ICardWatcherRepository>(_ => new CardsTestCardWatcherRepository());
                services.AddScoped<HydraForge.Application.Cards.ICardRelationshipRepository>(_ => new CardsTestCardRelationshipRepository());
                services.AddScoped<HydraForge.Application.Projects.IProjectMemberRepository>(_ => new CardsTestProjectMemberRepository(members));
                services.AddScoped<HydraForge.Application.Auth.IUserRepository>(_ => new CardsTestUserRepository(users));
                services.AddScoped<HydraForge.Application.Projects.IProjectContextSnapshotRepository>(_ => new CardsTestSnapshotRepository());
                services.AddScoped<HydraForge.Application.Projects.IChatArchiveService>(_ => new CardsTestChatArchiveService());
                services.AddScoped<HydraForge.Application.ProjectSnapshots.IProjectSnapshotRefresher>(_ => new TestSnapshotRefresher());
                services.AddScoped<IAuditLogWriter>(_ => new CardsTestAuditLogWriter());
                services.AddScoped<HydraForge.Application.Realtime.IProjectBoardEventPublisher, HydraForge.Infrastructure.Realtime.SignalRProjectBoardEventPublisher>();
                services.AddScoped<HydraForge.Application.Projects.ProjectService>();
                services.AddScoped<HydraForge.Application.Columns.ColumnService>();
                services.AddScoped<HydraForge.Application.Cards.CardService>();
                services.AddScoped<HydraForge.Application.Projects.ProjectMemberService>();
                services.AddScoped<HydraForge.Application.Cards.CardRelationshipService>();
            });
        }
    }

    [Fact]
    public async Task BoardEvent_Published_WhenCardCreated()
    {
        var factory = new BoardHubIntegrationTestFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = factory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(new HydraForge.Domain.Entities.ProjectSpace.Project { Id = projectId, Name = "Test Project" });
        factory.AddMember(new HydraForge.Domain.Entities.ProjectSpace.ProjectMember { ProjectId = projectId, UserId = userId, Role = HydraForge.Domain.Enums.MemberRole.Member });
        factory.AddColumn(new HydraForge.Domain.Entities.ProjectSpace.Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, "hubs/board"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var tcs = new TaskCompletionSource<HydraForge.Application.Realtime.ProjectBoardEventEnvelope>();

        hubConnection.On<HydraForge.Application.Realtime.ProjectBoardEventEnvelope>(
            "OnBoardEvent",
            envelope => tcs.TrySetResult(envelope));

        await hubConnection.StartAsync();
        try
        {
            await hubConnection.InvokeAsync("JoinProject", projectId);

            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards")
            {
                Content = new StringContent(
                    $"{{\"columnId\":\"{columnId}\",\"title\":\"New Card\",\"description\":\"\",\"type\":\"Task\",\"parentCardId\":null,\"dueAt\":null}}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                ),
            };
            request.Headers.Add("Authorization", $"Bearer {token}");

            var response = await client.SendAsync(request);
            Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

            var envelope = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HydraForge.Application.Realtime.BoardAction.Created, envelope.Action);
            Assert.Equal(HydraForge.Application.Realtime.BoardEntityType.Card, envelope.EntityType);
        }
        finally
        {
            await hubConnection.DisposeAsync();
        }
    }
}
