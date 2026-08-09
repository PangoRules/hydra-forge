namespace HydraForge.Infrastructure.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Chat;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class EfChatMessageRepositoryTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions(
        string? connectionString = null
    )
    {
        var connString =
            connectionString
            ?? "Host=localhost;Database=hydraforge_test;Username=postgres;Password=password";

        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql(connString, o => o.UseVector())
            .Options;
    }

    [Fact]
    public void Implements_IChatMessageRepository()
    {
        var options = CreateOptions();
        using var context = new HydraForgeDbContext(options);
        var repo = new EfChatMessageRepository(context);
        Assert.IsAssignableFrom<IChatMessageRepository>(repo);
    }

    [Fact]
    public async Task SearchByContentAsync_ScopeParticipated_IncludesProjectMemberSessionMatch()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfChatMessageRepository(context);

        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Title = "Project chat",
            Status = ChatSessionStatus.Active,
        };
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "Let's discuss the widget rollout.",
        };
        var projectMember = new HydraForge.Domain.Entities.ProjectSpace.ProjectMember
        {
            ProjectId = projectId,
            UserId = memberId,
            Role = HydraForge.Domain.Enums.MemberRole.Member,
        };

        context.ChatSessions.Add(session);
        context.ChatMessages.Add(message);
        context.ProjectMembers.Add(projectMember);
        await context.SaveChangesAsync();

        var mineResults = await repo.SearchByContentAsync(memberId, "widget", null, 20);
        Assert.Empty(mineResults);

        var participatedResults = await repo.SearchByContentAsync(
            memberId,
            "widget",
            null,
            20,
            ChatSessionScope.Participated
        );
        Assert.Single(participatedResults);
        Assert.Equal(message.Id, participatedResults[0].Id);
    }
}
