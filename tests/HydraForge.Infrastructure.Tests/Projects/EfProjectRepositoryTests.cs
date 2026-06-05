namespace HydraForge.Infrastructure.Tests.Projects;

using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using HydraForge.Infrastructure.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

public class EfProjectRepositoryTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions(string? connectionString = null)
    {
        var connString = connectionString
            ?? "Host=localhost;Database=hydraforge_test;Username=postgres;Password=password";

        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql(connString, o => o.UseVector())
            .Options;
    }

    [Fact]
    public void Implements_IProjectRepository()
    {
        var options = CreateOptions();
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectRepository(context);

        Assert.True(repo is IProjectRepository);
    }

    [Fact]
    public async Task AddAsync_InsertsProject()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectRepository(context);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "Description"
        };

        await repo.AddAsync(project);

        var saved = await context.Projects.FindAsync(project.Id);
        Assert.NotNull(saved);
        Assert.Equal("Test Project", saved.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingProject_ReturnsProjectWithColumns()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectRepository(context);

        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "Find Test", Description = "Desc" };
        var column = new Column { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Backlog", Position = 0 };
        context.Projects.Add(project);
        context.Columns.Add(column);
        await context.SaveChangesAsync();

        var result = await repo.GetByIdAsync(projectId);

        Assert.NotNull(result);
        Assert.Equal("Find Test", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesProject()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectRepository(context);

        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "Update Test", Description = "Desc" };
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        project.Name = "Updated Name";
        await repo.UpdateAsync(project);

        var saved = await context.Projects.FindAsync(projectId);
        Assert.NotNull(saved);
        Assert.Equal("Updated Name", saved.Name);
    }
}

public class EfProjectMemberRepositoryTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions(string? connectionString = null)
    {
        var connString = connectionString
            ?? "Host=localhost;Database=hydraforge_test;Username=postgres;Password=password";

        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql(connString, o => o.UseVector())
            .Options;
    }

    [Fact]
    public void Implements_IProjectMemberRepository()
    {
        var options = CreateOptions();
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectMemberRepository(context);

        Assert.True(repo is IProjectMemberRepository);
    }

    [Fact]
    public async Task AddMemberAsync_InsertsMember()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectMemberRepository(context);

        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "Member Test", Description = "Desc" };
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var member = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = Guid.NewGuid(),
            Role = MemberRole.Owner
        };

        await repo.AddMemberAsync(member);

        var saved = await context.ProjectMembers.FindAsync(member.Id);
        Assert.NotNull(saved);
        Assert.Equal(MemberRole.Owner, saved.Role);
    }

    [Fact]
    public async Task ListMembersAsync_ReturnsProjectMembers()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectMemberRepository(context);

        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "List Members Test", Description = "Desc" };
        var member1 = new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = Guid.NewGuid(), Role = MemberRole.Owner };
        var member2 = new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = Guid.NewGuid(), Role = MemberRole.Member };
        context.Projects.Add(project);
        context.ProjectMembers.AddRange(member1, member2);
        await context.SaveChangesAsync();

        var result = await repo.ListMembersAsync(projectId);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task RemoveMemberAsync_DeletesMember()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectMemberRepository(context);

        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "Remove Test", Description = "Desc" };
        var member = new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = Guid.NewGuid(), Role = MemberRole.Member };
        context.Projects.Add(project);
        context.ProjectMembers.Add(member);
        await context.SaveChangesAsync();

        await repo.RemoveMemberAsync(member.Id);

        var saved = await context.ProjectMembers.FindAsync(member.Id);
        Assert.Null(saved);
    }
}

public class EfChatArchiveServiceTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions(string? connectionString = null)
    {
        var connString = connectionString
            ?? "Host=localhost;Database=hydraforge_test;Username=postgres;Password=password";

        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql(connString, o => o.UseVector())
            .Options;
    }

    [Fact]
    public void Implements_IChatArchiveService()
    {
        var options = CreateOptions();
        using var context = new HydraForgeDbContext(options);
        var service = new EfChatArchiveService(context);

        Assert.True(service is IChatArchiveService);
    }

    [Fact]
    public async Task ArchiveProjectAsync_SetsArchivedAtOnFolderAndSessions()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var service = new EfChatArchiveService(context);

        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "Archive Chat Test", Description = "Desc" };
        var folder = new ChatFolder { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Project Chat", OwnerId = Guid.NewGuid() };
        var session = new ChatSession { Id = Guid.NewGuid(), ProjectId = projectId, FolderId = folder.Id, Title = "Chat", OwnerId = Guid.NewGuid() };
        context.Projects.Add(project);
        context.ChatFolders.Add(folder);
        context.ChatSessions.Add(session);
        await context.SaveChangesAsync();

        await service.ArchiveProjectAsync(projectId);

        var updatedFolder = await context.ChatFolders.FindAsync(folder.Id);
        var updatedSession = await context.ChatSessions.FindAsync(session.Id);
        Assert.NotNull(updatedFolder?.ArchivedAt);
        Assert.NotNull(updatedSession?.ArchivedAt);
    }
}

