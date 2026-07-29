namespace HydraForge.Server.Tests.Realtime;

using System.Reflection;
using HydraForge.Application.Audit;
using HydraForge.Application.Notifications;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Enums;
using HydraForge.Server.Tests.Projects;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

public class BoardHubIntegrationTests
{
    private class BoardHubIntegrationTestFactory : CardsTestWebApplicationFactory
    {
        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder
        )
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
                            d.ServiceType == typeof(Application.Projects.ProjectService)
                            || d.ServiceType == typeof(Application.Columns.ColumnService)
                            || d.ServiceType == typeof(Application.Cards.CardService)
                            || d.ServiceType == typeof(Application.Projects.IProjectRepository)
                            || d.ServiceType == typeof(Application.Projects.IColumnRepository)
                            || d.ServiceType == typeof(Application.Cards.ICardRepository)
                            || d.ServiceType == typeof(Application.Cards.ICardAssigneeRepository)
                            || d.ServiceType == typeof(Application.Cards.ICardWatcherRepository)
                            || d.ServiceType
                                == typeof(Application.Cards.ICardRelationshipRepository)
                            || d.ServiceType
                                == typeof(Application.Projects.IProjectMemberRepository)
                            || d.ServiceType == typeof(Application.Auth.IUserRepository)
                            || d.ServiceType
                                == typeof(Application.Projects.IProjectContextSnapshotRepository)
                            || d.ServiceType == typeof(Application.Projects.IChatArchiveService)
                            || d.ServiceType
                                == typeof(Application.ProjectSnapshots.IProjectSnapshotRefresher)
                        )
                        .ToList()
                )
                {
                    services.Remove(descriptor);
                }

                var factoryType = typeof(CardsTestWebApplicationFactory);
                var projects =
                    (List<Domain.Entities.ProjectSpace.Project>)
                        factoryType
                            .GetField("_projects", BindingFlags.NonPublic | BindingFlags.Instance)!
                            .GetValue(this)!;
                var columns =
                    (List<Domain.Entities.ProjectSpace.Column>)
                        factoryType
                            .GetField("_columns", BindingFlags.NonPublic | BindingFlags.Instance)!
                            .GetValue(this)!;
                var cards =
                    (List<Domain.Entities.ProjectSpace.Card>)
                        factoryType
                            .GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance)!
                            .GetValue(this)!;
                var members =
                    (List<Domain.Entities.ProjectSpace.ProjectMember>)
                        factoryType
                            .GetField("_members", BindingFlags.NonPublic | BindingFlags.Instance)!
                            .GetValue(this)!;
                var users =
                    (List<Domain.Entities.Auth.User>)
                        factoryType
                            .GetField("_users", BindingFlags.NonPublic | BindingFlags.Instance)!
                            .GetValue(this)!;
                var cardAssignees =
                    (List<Domain.Entities.ProjectSpace.CardAssignee>)
                        factoryType
                            .GetField(
                                "_cardAssignees",
                                BindingFlags.NonPublic | BindingFlags.Instance
                            )!
                            .GetValue(this)!;

                services.AddScoped<Application.Projects.IProjectRepository>(
                    _ => new CardsTestProjectRepository(projects)
                );
                services.AddScoped<Application.Projects.IColumnRepository>(
                    _ => new CardsTestColumnRepository(columns)
                );
                services.AddScoped<Application.Cards.ICardRepository>(
                    _ => new CardsTestCardRepository(cards)
                );
                services.AddScoped<Application.Cards.ICardAssigneeRepository>(
                    _ => new CardsTestCardAssigneeRepository(cardAssignees)
                );
                services.AddScoped<Application.Cards.ICardWatcherRepository>(
                    _ => new CardsTestCardWatcherRepository()
                );
                services.AddScoped<Application.Cards.ICardRelationshipRepository>(
                    _ => new CardsTestCardRelationshipRepository()
                );
                services.AddScoped<Application.Projects.IProjectMemberRepository>(
                    _ => new CardsTestProjectMemberRepository(members)
                );
                services.AddScoped<Application.Auth.IUserRepository>(
                    _ => new CardsTestUserRepository(users)
                );
                services.AddScoped<Application.Projects.IProjectContextSnapshotRepository>(
                    _ => new CardsTestSnapshotRepository()
                );
                services.AddScoped<Application.Projects.IChatArchiveService>(
                    _ => new CardsTestChatArchiveService()
                );
                services.AddScoped<Application.ProjectSnapshots.IProjectSnapshotRefresher>(
                    _ => new TestSnapshotRefresher()
                );
                services.AddScoped<IAuditLogWriter>(_ => new CardsTestAuditLogWriter());
                services.AddScoped<
                    IProjectBoardEventPublisher,
                    Infrastructure.Realtime.SignalRProjectBoardEventPublisher
                >();
                services.AddScoped<INotificationService>(_ => new FakeNotificationService());
                services.AddScoped<Application.Projects.ProjectService>();
                services.AddScoped<Application.Columns.ColumnService>();
                services.AddScoped<Application.Cards.CardService>();
                services.AddScoped<Application.Projects.ProjectMemberService>();
                services.AddScoped<Application.Cards.CardRelationshipService>();
            });
        }
    }

    [Fact]
    public async Task BoardEvent_Published_WhenCardCreated()
    {
        var factory = new BoardHubIntegrationTestFactory();
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = CardsTestWebApplicationFactory.IssueToken(userId, "member", isAdmin: false);
        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        factory.AddProject(
            new Domain.Entities.ProjectSpace.Project { Id = projectId, Name = "Test Project" }
        );
        factory.AddMember(
            new Domain.Entities.ProjectSpace.ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Member,
            }
        );
        factory.AddColumn(
            new Domain.Entities.ProjectSpace.Column
            {
                Id = columnId,
                ProjectId = projectId,
                Name = "Backlog",
                Position = 0,
            }
        );

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress!, "hubs/board"),
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token)!;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                }
            )
            .AddJsonProtocol(o =>
                o.PayloadSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter()
                )
            )
            .Build();

        var tcs = new TaskCompletionSource<ProjectBoardEventEnvelope>();

        hubConnection.On<ProjectBoardEventEnvelope>(
            "OnBoardEvent",
            envelope => tcs.TrySetResult(envelope)
        );

        await hubConnection.StartAsync();
        try
        {
            await hubConnection.InvokeAsync("JoinProject", projectId);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/projects/{projectId}/cards"
            )
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
            Assert.Equal(BoardAction.Created, envelope.Action);
            Assert.Equal(BoardEntityType.Card, envelope.EntityType);
        }
        finally
        {
            await hubConnection.DisposeAsync();
        }
    }
}
