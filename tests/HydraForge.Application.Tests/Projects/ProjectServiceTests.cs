using HydraForge.Application.Audit;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.Projects;

public class ProjectServiceTests
{
    private static CreateProjectCommand DefaultCreateCmd(Guid ownerId) =>
        new(ownerId, "Test Project", "A test project", null, null, ColumnTemplate.General);

    [Fact]
    public async Task CreateAsync_ValidCommand_ReturnsProjectWithOwnerMember()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal("Test Project", result.Value.Name);
        Assert.Single(result.Value.Members);
        Assert.Equal(MemberRole.Owner, result.Value.Members[0].Role);
    }

    [Fact]
    public async Task CreateAsync_DefaultTemplate_InsertsFourGeneralColumns()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Columns.Count);
        Assert.Equal("Backlog", result.Value.Columns[0].Name);
        Assert.Equal("Done", result.Value.Columns[3].Name);
    }

    [Fact]
    public async Task CreateAsync_SoftwareTemplate_InsertsSixColumns()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var cmd = DefaultCreateCmd(Guid.NewGuid());
        cmd = cmd with { Template = ColumnTemplate.Software };

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Columns.Count);
        Assert.Equal("Backlog", result.Value.Columns[0].Name);
        Assert.Equal("Done", result.Value.Columns[5].Name);
    }

    [Fact]
    public async Task CreateAsync_BlankTemplate_InsertsTwoColumns()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var cmd = DefaultCreateCmd(Guid.NewGuid());
        cmd = cmd with { Template = ColumnTemplate.Blank };

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Columns.Count);
        Assert.Equal("To Do", result.Value.Columns[0].Name);
        Assert.Equal("Done", result.Value.Columns[1].Name);
    }

    [Fact]
    public async Task CreateAsync_CreatesSnapshot()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        await handler.CreateAsync(cmd);

        Assert.Single(snapshotRepo.AddedSnapshots);
        Assert.NotNull(snapshotRepo.AddedSnapshots[0].TemplateContent);
    }

    [Fact]
    public async Task CreateAsync_CreatesProjectChatFolder()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetByIdAsync_NonMember_ReturnsMembershipDenied()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
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
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );

        var result = await handler.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ArchivedProject_ReturnsProject()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        repo.Projects.Add(
            new Project
            {
                Id = projectId,
                Name = "Archived Project",
                ArchivedAt = DateTime.UtcNow,
            }
        );
        memberRepo.Members.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Owner,
            }
        );

        var result = await handler.GetByIdAsync(projectId, userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_OwnerArchives_CallsChatArchiveService()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = ownerId,
                Role = MemberRole.Owner,
            }
        );

        var result = await handler.ToggleArchiveAsync(
            new ToggleProjectArchiveCommand(projectId, ownerId)
        );

        Assert.True(result.IsSuccess);
        Assert.Contains(projectId, chatService.ArchivedProjectIds);
        Assert.NotNull(repo.Projects.First(p => p.Id == projectId).ArchivedAt);
    }

    [Fact]
    public async Task CreateProject_WritesAuditLog()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var ownerId = Guid.NewGuid();
        var cmd = new CreateProjectCommand(
            ownerId,
            "Audit Test Project",
            "Testing audit",
            null,
            null
        );

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
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        repo.Projects.Add(
            new Project
            {
                Id = projectId,
                Name = "Original",
                Description = "Original desc",
            }
        );
        memberRepo.Members.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Owner,
            }
        );

        var result = await handler.UpdateAsync(
            new UpdateProjectCommand(projectId, actorId, "Updated", "Updated desc", null, null)
        );

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
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "To Archive" });
        memberRepo.Members.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = ownerId,
                Role = MemberRole.Owner,
            }
        );

        var result = await handler.ToggleArchiveAsync(
            new ToggleProjectArchiveCommand(projectId, ownerId)
        );

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
    public async Task GetByIdAsync_AdminNonMember_Succeeds()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateAdminMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var projectId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Private Project" });

        var result = await handler.GetByIdAsync(projectId, adminId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Private Project", result.Value.Name);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPagedProjectsWithMyRole()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        repo.Projects.Add(
            new Project
            {
                Id = projectId,
                Name = "Test Project",
                Description = "d",
            }
        );
        memberRepo.Members.Add(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                Role = MemberRole.Owner,
            }
        );

        var result = await handler.GetAllAsync(
            userId,
            includeArchived: false,
            search: null,
            sortBy: ProjectSortField.Name,
            sortDescending: false,
            role: null,
            skip: 0,
            take: 20
        );

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(MemberRole.Owner, result.Value.Items[0].MyRole);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_AdminWithRoleFilter_RoutesToListByUserIdAsync()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateAdminMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var adminId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "Test Project" });

        await handler.GetAllAsync(
            adminId,
            includeArchived: false,
            search: null,
            sortBy: ProjectSortField.Name,
            sortDescending: false,
            role: MemberRole.Owner,
            skip: 0,
            take: 20,
            isAdmin: true
        );

        Assert.Equal(MemberRole.Owner, repo.LastRoleFilter);
        Assert.Equal(1, repo.ListByUserIdAsyncCallCount);
        Assert.Equal(0, repo.ListAllAsyncCallCount);
    }

    [Fact]
    public async Task GetAllAsync_AdminWithoutRoleFilter_RoutesToListAllAsync()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateAdminMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var adminId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "Test Project" });

        await handler.GetAllAsync(
            adminId,
            includeArchived: false,
            search: null,
            sortBy: ProjectSortField.Name,
            sortDescending: false,
            role: null,
            skip: 0,
            take: 20,
            isAdmin: true
        );

        Assert.Equal(0, repo.ListByUserIdAsyncCallCount);
        Assert.Equal(1, repo.ListAllAsyncCallCount);
    }

    [Fact]
    public async Task GetAllAsync_ClampsTakeToMaxOneHundred()
    {
        var (
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        ) = CreateMocks();
        var handler = new ProjectService(
            repo,
            columnRepo,
            memberRepo,
            snapshotRepo,
            chatService,
            snapshotRefresher,
            publisher,
            auditWriter,
            userRepo,
            notifService
        );
        var userId = Guid.NewGuid();

        var result = await handler.GetAllAsync(
            userId,
            includeArchived: false,
            search: null,
            sortBy: ProjectSortField.Name,
            sortDescending: false,
            role: null,
            skip: 0,
            take: 9999
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(100, repo.LastTake);
    }

    private static (
        InMemoryProjectRepository repo,
        InMemoryColumnRepository columnRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemorySnapshotRepository snapshotRepo,
        InMemoryChatArchiveService chatService,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher,
        InMemoryAuditLogWriter auditWriter,
        InMemoryUserRepository userRepo,
        FakeNotificationService notifService
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
            new InMemoryAuditLogWriter(),
            new InMemoryUserRepository(),
            new FakeNotificationService()
        );
    }

    private static (
        InMemoryProjectRepository repo,
        InMemoryColumnRepository columnRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemorySnapshotRepository snapshotRepo,
        InMemoryChatArchiveService chatService,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher,
        InMemoryAuditLogWriter auditWriter,
        FakeAdminUserRepo userRepo,
        FakeNotificationService notifService
    ) CreateAdminMocks()
    {
        return (
            new InMemoryProjectRepository(),
            new InMemoryColumnRepository(),
            new InMemoryProjectMemberRepository(),
            new InMemorySnapshotRepository(),
            new InMemoryChatArchiveService(),
            new NullSnapshotRefresher(),
            new FakeProjectBoardEventPublisher(),
            new InMemoryAuditLogWriter(),
            new FakeAdminUserRepo(),
            new FakeNotificationService()
        );
    }
}

internal class InMemoryProjectRepository : IProjectRepository
{
    public List<Project> Projects { get; } = [];
    public Dictionary<Guid, HashSet<Guid>> UserMemberships { get; } = [];
    public int LastTake { get; private set; }
    public int ListAllAsyncCallCount { get; private set; }
    public int ListByUserIdAsyncCallCount { get; private set; }
    public MemberRole? LastRoleFilter { get; private set; }

    public void AddMembership(Guid userId, Guid projectId)
    {
        if (!UserMemberships.TryGetValue(userId, out var set))
        {
            set = [];
            UserMemberships[userId] = set;
        }
        set.Add(projectId);
    }

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        Projects.Add(project);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Project>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Project>>(Projects);

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Projects.FirstOrDefault(p => p.Id == id));

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
        LastTake = take;
        ListByUserIdAsyncCallCount++;
        LastRoleFilter = role;

        var filtered = Projects.AsEnumerable();
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
        var idx = Projects.FindIndex(p => p.Id == project.Id);
        if (idx >= 0)
            Projects[idx] = project;
        return Task.CompletedTask;
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
        ListAllAsyncCallCount++;

        var filtered = Projects.AsEnumerable();
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

    public Task<ProjectListPage> ListNonMemberProjectsAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        var memberProjectIds = UserMemberships.TryGetValue(userId, out var set) ? set : [];

        var filtered = Projects.Where(p => !memberProjectIds.Contains(p.Id)).AsEnumerable();

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
}

internal class InMemoryColumnRepository : IColumnRepository
{
    public List<Column> Columns { get; } = [];

    public Task AddAsync(Column column, CancellationToken ct = default)
    {
        Columns.Add(column);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Column>>([.. Columns.Where(c => c.ProjectId == projectId)]);

    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Columns.FirstOrDefault(c => c.Id == id));

    public Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        var idx = Columns.FindIndex(c => c.Id == column.Id);
        if (idx >= 0)
            Columns[idx] = column;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Columns.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    public Task ReorderAsync(
        Guid projectId,
        IReadOnlyList<Guid> orderedColumnIds,
        CancellationToken ct = default
    )
    {
        for (var i = 0; i < orderedColumnIds.Count; i++)
        {
            var col = Columns.FirstOrDefault(c => c.Id == orderedColumnIds[i]);
            col?.Position = i;
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

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Members.FirstOrDefault(m => m.Id == id));

    public Task<ProjectMember?> GetByProjectAndUserAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            Members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId)
        );

    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
        Guid projectId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<ProjectMember>>([
            .. Members.Where(m => m.ProjectId == projectId),
        ]);

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        var counts = Members
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
        var roles = Members
            .Where(m => idList.Contains(m.ProjectId) && m.UserId == userId)
            .ToDictionary(m => m.ProjectId, m => m.Role);
        return Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(roles);
    }

    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
    {
        Members.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }

    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = Members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0)
            Members[idx] = member;
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

    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken ct = default
    ) => Task.FromResult<ProjectContextSnapshot?>(null);

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

    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal class InMemoryUserRepository : Application.Auth.IUserRepository
{
    public Task<Domain.Entities.Auth.User?> FindByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) => Task.FromResult<Domain.Entities.Auth.User?>(null);

    public Task<IReadOnlyDictionary<Guid, Domain.Entities.Auth.User>> FindByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Domain.Entities.Auth.User>>(
            new Dictionary<Guid, Domain.Entities.Auth.User>()
        );

    public Task<Domain.Entities.Auth.User?> FindByUsernameAsync(string username) =>
        Task.FromResult<Domain.Entities.Auth.User?>(null);

    public Task<IReadOnlyDictionary<string, Domain.Entities.Auth.User>> FindByUsernamesAsync(
        IReadOnlyList<string> usernames,
        string? searchTerm = null,
        int maxResults = 10,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<string, Domain.Entities.Auth.User>>(
            new Dictionary<string, Domain.Entities.Auth.User>()
        );

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task CreateAsync(Domain.Entities.Auth.User user, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Domain.Entities.Auth.User>> ListAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Domain.Entities.Auth.User>>([]);

    public Task<int> CountAsync(string? search, CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task UpdateAsync(Domain.Entities.Auth.User user, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal sealed class FakeAdminUserRepo : Application.Auth.IUserRepository
{
    public Task<Domain.Entities.Auth.User?> FindByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) => Task.FromResult<Domain.Entities.Auth.User?>(null);

    public Task<IReadOnlyDictionary<Guid, Domain.Entities.Auth.User>> FindByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Domain.Entities.Auth.User>>(
            new Dictionary<Guid, Domain.Entities.Auth.User>()
        );

    public Task<Domain.Entities.Auth.User?> FindByUsernameAsync(string username) =>
        Task.FromResult<Domain.Entities.Auth.User?>(null);

    public Task<IReadOnlyDictionary<string, Domain.Entities.Auth.User>> FindByUsernamesAsync(
        IReadOnlyList<string> usernames,
        string? searchTerm = null,
        int maxResults = 10,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<string, Domain.Entities.Auth.User>>(
            new Dictionary<string, Domain.Entities.Auth.User>()
        );

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(true);

    public Task CreateAsync(Domain.Entities.Auth.User user, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<Domain.Entities.Auth.User>> ListAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Domain.Entities.Auth.User>>([]);

    public Task<int> CountAsync(string? search, CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task UpdateAsync(Domain.Entities.Auth.User user, CancellationToken ct = default) =>
        Task.CompletedTask;
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
