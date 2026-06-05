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
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
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
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
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
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        await handler.CreateAsync(cmd);

        Assert.Single(snapshotRepo.AddedSnapshots);
        Assert.NotNull(snapshotRepo.AddedSnapshots[0].TemplateContent);
    }

    [Fact]
    public async Task CreateAsync_CreatesProjectChatFolder()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetByIdAsync_NonMember_ReturnsMembershipDenied()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
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
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);

        var result = await handler.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ArchivedProject_ReturnsArchived()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Archived Project", ArchivedAt = DateTime.UtcNow });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Owner });

        var result = await handler.GetByIdAsync(projectId, userId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.Archived, result.Error.Code);
    }

    [Fact]
    public async Task AddMemberAsync_OwnerAddsMember_Success()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });

        var result = await handler.AddMemberAsync(new AddProjectMemberCommand(projectId, newMemberId, MemberRole.Member, ownerId));

        Assert.True(result.IsSuccess);
        Assert.Equal(newMemberId, result.Value.UserId);
        Assert.Equal(MemberRole.Member, result.Value.Role);
    }

    [Fact]
    public async Task AddMemberAsync_NonOwnerDenied_ReturnsOwnerRequired()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = memberId, Role = MemberRole.Member });

        var result = await handler.AddMemberAsync(new AddProjectMemberCommand(projectId, newMemberId, MemberRole.Member, memberId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.OwnerRequired, result.Error.Code);
    }

    [Fact]
    public async Task AddMemberAsync_DuplicateMember_ReturnsDuplicateError()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = existingMemberId, Role = MemberRole.Member });

        var result = await handler.AddMemberAsync(new AddProjectMemberCommand(projectId, existingMemberId, MemberRole.Member, ownerId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MemberDuplicate, result.Error.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_LastOwnerDenied_ReturnsLastOwnerRemovalDenied()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });

        var result = await handler.RemoveMemberAsync(new RemoveProjectMemberCommand(projectId, ownerId, ownerId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.LastOwnerRemovalDenied, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_OwnerArchives_CallsChatArchiveService()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, userRepo);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });

        var result = await handler.ArchiveAsync(new ArchiveProjectCommand(projectId, ownerId));

        Assert.True(result.IsSuccess);
        Assert.Contains(projectId, chatService.ArchivedProjectIds);
        Assert.NotNull(repo.Projects.First(p => p.Id == projectId).ArchivedAt);
    }

    private static (
        InMemoryProjectRepository repo,
        InMemoryColumnRepository columnRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemorySnapshotRepository snapshotRepo,
        InMemoryChatArchiveService chatService,
        InMemoryUserRepository userRepo
    ) CreateMocks()
    {
        return (
            new InMemoryProjectRepository(),
            new InMemoryColumnRepository(),
            new InMemoryProjectMemberRepository(),
            new InMemorySnapshotRepository(),
            new InMemoryChatArchiveService(),
            new InMemoryUserRepository()
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

    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default)
    {
        Columns.AddRange(columns);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Column>>(Columns.Where(c => c.ProjectId == projectId).ToList());
}

internal class InMemoryProjectMemberRepository : IProjectMemberRepository
{
    public List<ProjectMember> Members { get; } = [];

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        Members.Add(member);
        return Task.CompletedTask;
    }

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

    public Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        AddedSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<ProjectContextSnapshot?>(null);
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
    public Task<HydraForge.Domain.Entities.Auth.User?> FindByIdAsync(Guid id)
        => Task.FromResult<HydraForge.Domain.Entities.Auth.User?>(null);

    public Task<HydraForge.Domain.Entities.Auth.User?> FindByUsernameAsync(string username)
        => Task.FromResult<HydraForge.Domain.Entities.Auth.User?>(null);

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt)
        => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync()
        => Task.FromResult(false);

    public Task CreateAsync(HydraForge.Domain.Entities.Auth.User user)
        => Task.CompletedTask;
}