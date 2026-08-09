namespace HydraForge.Infrastructure.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Chat;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class EfChatSessionRepositoryTests
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
    public void Implements_IChatSessionRepository()
    {
        var options = CreateOptions();
        using var context = new HydraForgeDbContext(options);
        var repo = new EfChatSessionRepository(context);

        Assert.True(repo is IChatSessionRepository);
    }

    [Fact]
    public async Task ListAsync_CompositeCursor_DoesNotSkipSameTimestampSessions()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfChatSessionRepository(context);

        var userId = Guid.NewGuid();
        var sameTime = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

        // Seed 3 sessions with identical UpdatedAt, ordered by Id ascending
        var session1 = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "A",
            UpdatedAt = sameTime,
            CreatedAt = sameTime,
            Status = ChatSessionStatus.Active,
        };
        var session2 = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "B",
            UpdatedAt = sameTime,
            CreatedAt = sameTime,
            Status = ChatSessionStatus.Active,
        };
        var session3 = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "C",
            UpdatedAt = sameTime,
            CreatedAt = sameTime,
            Status = ChatSessionStatus.Active,
        };

        // Ensure Ids are ordered so session1 < session2 < session3 for deterministic test
        if (session2.Id.CompareTo(session1.Id) < 0)
            (session1, session2) = (session2, session1);
        if (session3.Id.CompareTo(session2.Id) < 0)
            (session2, session3) = (session3, session2);
        if (session2.Id.CompareTo(session1.Id) < 0)
            (session1, session2) = (session2, session1);

        context.ChatSessions.AddRange(session1, session2, session3);
        await context.SaveChangesAsync();

        // Page 1: before = DateTime.MaxValue, beforeId = null — should return all 3 ordered by UpdatedAt desc, Id desc
        var page1 = await repo.ListAsync(
            userId,
            null,
            null,
            DateTime.MaxValue,
            null,
            3,
            false,
            ChatSessionStatusFilter.NonArchived,
            CancellationToken.None
        );
        Assert.Equal(3, page1.Count);

        // Page 2: limit 2, before = sameTime, beforeId = page1[1].Id (the 2nd item = 2nd newest by UpdatedAt desc, Id desc)
        var secondItemId = page1[1].Id;
        var page2 = await repo.ListAsync(
            userId,
            null,
            null,
            sameTime,
            secondItemId,
            2,
            false,
            ChatSessionStatusFilter.NonArchived,
            CancellationToken.None
        );
        // Should get exactly 1 remaining item (session1 — the oldest by UpdatedAt==sameTime, Id smallest)
        Assert.Single(page2);
        Assert.DoesNotContain(page1[0].Id, page2.Select(s => s.Id));
        Assert.DoesNotContain(page1[1].Id, page2.Select(s => s.Id));
        Assert.Equal(page1[2].Id, page2[0].Id);
    }

    [Fact]
    public async Task ListAsync_OrdersByUpdatedAtDescendingThenByIdDescending()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfChatSessionRepository(context);

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session1 = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "Older",
            UpdatedAt = now.AddHours(-2),
            CreatedAt = now.AddHours(-2),
            Status = ChatSessionStatus.Active,
        };
        var session2 = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "Newer",
            UpdatedAt = now.AddHours(-1),
            CreatedAt = now.AddHours(-1),
            Status = ChatSessionStatus.Active,
        };

        context.ChatSessions.AddRange(session1, session2);
        await context.SaveChangesAsync();

        var results = await repo.ListAsync(
            userId,
            null,
            null,
            DateTime.MaxValue,
            null,
            10,
            false,
            ChatSessionStatusFilter.NonArchived,
            CancellationToken.None
        );

        Assert.Equal(2, results.Count);
        Assert.Equal(session2.Id, results[0].Id);
        Assert.Equal(session1.Id, results[1].Id);
    }

    [Fact]
    public async Task ListAsync_FiltersbyOwnerAndNotArchived()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfChatSessionRepository(context);

        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var mySession = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = "Mine",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Status = ChatSessionStatus.Active,
        };
        var otherSession = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = otherUserId,
            Title = "Other",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Status = ChatSessionStatus.Active,
        };
        var archivedSession = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = "Archived",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Status = ChatSessionStatus.Active,
            ArchivedAt = DateTime.UtcNow,
        };

        context.ChatSessions.AddRange(mySession, otherSession, archivedSession);
        await context.SaveChangesAsync();

        var results = await repo.ListAsync(
            ownerId,
            null,
            null,
            DateTime.MaxValue,
            null,
            10,
            false,
            ChatSessionStatusFilter.NonArchived,
            CancellationToken.None
        );

        Assert.Single(results);
        Assert.Equal(mySession.Id, results[0].Id);
    }

    [Fact]
    public async Task ListAsync_WhereParticipatedIn_IncludesProjectMemberSessions()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfChatSessionRepository(context);

        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var projectSession = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            ProjectId = projectId,
            Title = "Project Session",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Status = ChatSessionStatus.Active,
        };
        var personalSession = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            ProjectId = null,
            Title = "Personal Session",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Status = ChatSessionStatus.Active,
        };

        var projectMember = new HydraForge.Domain.Entities.ProjectSpace.ProjectMember
        {
            ProjectId = projectId,
            UserId = memberId,
            Role = HydraForge.Domain.Enums.MemberRole.Member,
        };

        context.ChatSessions.AddRange(projectSession, personalSession);
        context.ProjectMembers.Add(projectMember);
        await context.SaveChangesAsync();

        // Member should see the project session (via ProjectMember participation)
        var memberResults = await repo.ListAsync(
            memberId,
            null,
            null,
            DateTime.MaxValue,
            null,
            10,
            false,
            ChatSessionStatusFilter.NonArchived,
            CancellationToken.None
        );
        Assert.Contains(memberResults, s => s.Id == projectSession.Id);

        // Non-member should NOT see the project session
        var nonMemberResults = await repo.ListAsync(
            nonMemberId,
            null,
            null,
            DateTime.MaxValue,
            null,
            10,
            false,
            ChatSessionStatusFilter.NonArchived,
            CancellationToken.None
        );
        Assert.DoesNotContain(nonMemberResults, s => s.Id == projectSession.Id);
    }
}
