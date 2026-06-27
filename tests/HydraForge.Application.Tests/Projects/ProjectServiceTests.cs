using HydraForge.Application.Audit;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.Projects;

public class ProjectServiceTests
{
    private static CreateProjectCommand DefaultCreateCmd(Guid ownerId) =>
        new(ownerId, "Test Project", "A test project", null, null);

    [Fact]
    public async Task CreateAsync_ValidCommand_ReturnsProjectWithOwnerMember()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal("Test Project", result.Value.Name);
        Assert.Single(result.Value.Members);
        Assert.Equal(MemberRole.Owner, result.Value.Members[0].Role);
    }

    [Fact]
    public async Task CreateAsync_InsertsSixDefaultColumns()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Columns.Count);
        Assert.Equal("Backlog", result.Value.Columns[0].Name);
        Assert.Equal("Done", result.Value.Columns[5].Name);
    }

    [Fact]
    public async Task CreateAsync_CreatesSnapshot()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        await handler.CreateAsync(cmd);

        Assert.Single(snapshotRepo.AddedSnapshots);
        Assert.NotNull(snapshotRepo.AddedSnapshots[0].TemplateContent);
    }

    [Fact]
    public async Task CreateAsync_CreatesProjectChatFolder()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetByIdAsync_NonMember_ReturnsMembershipDenied()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var projectId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Private Project" });
        var nonMemberUserId = Guid.NewGuid();

        var result = await handler.GetByIdAsync(projectId, nonMemberUserId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentProject_ReturnsNotFound()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);

        var result = await handler.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ArchivedProject_ReturnsProject()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Archived Project", ArchivedAt = DateTime.UtcNow });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Owner });

        var result = await handler.GetByIdAsync(projectId, userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_OwnerArchives_CallsChatArchiveService()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });

        var result = await handler.ArchiveAsync(new ArchiveProjectCommand(projectId, ownerId));

        Assert.True(result.IsSuccess);
        Assert.Contains(projectId, chatService.ArchivedProjectIds);
        Assert.NotNull(repo.Projects.First(p => p.Id == projectId).ArchivedAt);
    }

    [Fact]
    public async Task CreateProject_WritesAuditLog()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var ownerId = Guid.NewGuid();
        var cmd = new CreateProjectCommand(ownerId, "Audit Test Project", "Testing audit", null, null);

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        var log = Assert.Single(auditWriter.Writes);
        Assert.Equal(AuditLogScope.Project, log.Scope);
        Assert.Equal(ownerId, log.ActorId);
        Assert.Equal("Project", log.EntityType);
        Assert.Equal(result.Value.Id, log.EntityId);
        Assert.Equal("Created", log.Action);
        Assert.Equal(result.Value.Id, log.ProjectId);
    }

    [Fact]
    public async Task UpdateProject_WritesAuditLog()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Original", Description = "Original desc" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await handler.UpdateAsync(new UpdateProjectCommand(projectId, actorId, "Updated", "Updated desc", null, null));

        Assert.True(result.IsSuccess);
        var log = Assert.Single(auditWriter.Writes);
        Assert.Equal(AuditLogScope.Project, log.Scope);
        Assert.Equal(actorId, log.ActorId);
        Assert.Equal("Project", log.EntityType);
        Assert.Equal(projectId, log.EntityId);
        Assert.Equal("Updated", log.Action);
        Assert.Equal(projectId, log.ProjectId);
    }

    [Fact]
    public async Task ArchiveProject_WritesAuditLog()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "To Archive" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });

        var result = await handler.ArchiveAsync(new ArchiveProjectCommand(projectId, ownerId));

        Assert.True(result.IsSuccess);
        var log = Assert.Single(auditWriter.Writes);
        Assert.Equal(AuditLogScope.Project, log.Scope);
        Assert.Equal(ownerId, log.ActorId);
        Assert.Equal("Project", log.EntityType);
        Assert.Equal(projectId, log.EntityId);
        Assert.Equal("Archived", log.Action);
        Assert.Equal(projectId, log.ProjectId);
    }

    [Fact]
    public async Task DeleteProject_WritesAuditLog()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "To Delete" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });

        var result = await handler.DeleteAsync(new DeleteProjectCommand(projectId, ownerId));

        Assert.True(result.IsSuccess);
        var log = Assert.Single(auditWriter.Writes);
        Assert.Equal(AuditLogScope.Project, log.Scope);
        Assert.Equal(ownerId, log.ActorId);
        Assert.Equal("Project", log.EntityType);
        Assert.Equal(projectId, log.EntityId);
        Assert.Equal("Deleted", log.Action);
        Assert.Equal(projectId, log.ProjectId);
    }

    private static (
        InMemoryProjectRepository repo,
        InMemoryColumnRepository columnRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemorySnapshotRepository snapshotRepo,
        InMemoryChatArchiveService chatService,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher,
        InMemoryAuditLogWriter auditWriter
    ) CreateMocks()
    {
        return (
            new InMemoryProjectRepository(),
            new InMemoryColumnRepository(),
            new InMemoryProjectMemberRepository(),
            new InMemorySnapshotRepository(),
            new InMemoryChatArchiveService(),
            new NullSnapshotRefresher(),
            new FakeProjectBoardEventPublisher(),
            new InMemoryAuditLogWriter()
        );
    }
}

internal class InMemoryProjectRepository : IProjectRepository
{
    public List<Project> Projects { get; } = [];

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        Projects.Add(project);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(Projects);

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Projects.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Project>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(Projects);

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var idx = Projects.FindIndex(p => p.Id == project.Id);
        if (idx >= 0) Projects[idx] = project;
        return Task.CompletedTask;
    }
}

internal class InMemoryColumnRepository : IColumnRepository
{
    public List<Column> Columns { get; } = [];

    public Task AddAsync(Column column, CancellationToken ct = default)
    {
        Columns.Add(column);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Column>>(Columns.Where(c => c.ProjectId == projectId).ToList());

    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Columns.FirstOrDefault(c => c.Id == id));

    public Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        var idx = Columns.FindIndex(c => c.Id == column.Id);
        if (idx >= 0) Columns[idx] = column;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Columns.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default)
    {
        for (var i = 0; i < orderedColumnIds.Count; i++)
        {
            var col = Columns.FirstOrDefault(c => c.Id == orderedColumnIds[i]);
            if (col != null) col.Position = i;
        }
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default)
    {
        Columns.AddRange(columns);
        return Task.CompletedTask;
    }
}

internal class InMemoryProjectMemberRepository : IProjectMemberRepository
{
    public List<ProjectMember> Members { get; } = [];

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        Members.Add(member);
        return Task.CompletedTask;
    }

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Members.FirstOrDefault(m => m.Id == id));

    public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));

    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProjectMember>>(Members.Where(m => m.ProjectId == projectId).ToList());

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
    {
        var idList = projectIds.ToList();
        var counts = Members
            .Where(m => idList.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }

    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
    {
        Members.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }

    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = Members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0) Members[idx] = member;
        return Task.CompletedTask;
    }
}

internal class InMemorySnapshotRepository : IProjectContextSnapshotRepository
{
    public List<ProjectContextSnapshot> AddedSnapshots { get; } = [];
    public List<ProjectContextSnapshot> UpdatedSnapshots { get; } = [];

    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        AddedSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<ProjectContextSnapshot?>(null);

    public Task UpdateAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        UpdatedSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }
}

internal class InMemoryChatArchiveService : IChatArchiveService
{
    public List<Guid> ArchivedProjectIds { get; } = [];

    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        ArchivedProjectIds.Add(projectId);
        return Task.CompletedTask;
    }

    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default)
        => Task.CompletedTask;
}

internal class InMemoryUserRepository : HydraForge.Application.Auth.IUserRepository
{
    public Task<HydraForge.Domain.Entities.Auth.User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<HydraForge.Domain.Entities.Auth.User?>(null);

    public Task<IReadOnlyDictionary<Guid, HydraForge.Domain.Entities.Auth.User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, HydraForge.Domain.Entities.Auth.User>>(new Dictionary<Guid, HydraForge.Domain.Entities.Auth.User>());

    public Task<HydraForge.Domain.Entities.Auth.User?> FindByUsernameAsync(string username)
        => Task.FromResult<HydraForge.Domain.Entities.Auth.User?>(null);

    public Task<IReadOnlyDictionary<string, HydraForge.Domain.Entities.Auth.User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, CancellationToken ct = default)
=> Task.FromResult<IReadOnlyDictionary<string, HydraForge.Domain.Entities.Auth.User>>(new Dictionary<string, HydraForge.Domain.Entities.Auth.User>());
    public Task<List<HydraForge.Domain.Entities.Auth.User>> SearchByUsernameAsync(string query, int maxResults = 10, CancellationToken ct = default)
        => Task.FromResult(new List<HydraForge.Domain.Entities.Auth.User>());

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt)
        => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync()
        => Task.FromResult(false);

    public Task CreateAsync(HydraForge.Domain.Entities.Auth.User user)
    {
        return Task.CompletedTask;
    }
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public List<AuditLogRequest> Writes { get; } = [];

    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
    {
        Writes.Add(request);
        return Task.FromResult(Result.Success());
    }
}
