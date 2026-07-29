using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.Projects;

public class ProjectMemberServiceTests
{
    private sealed class FakeUserRepoForAdmin : IUserRepository
    {
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<Guid, User>>(new Dictionary<Guid, User>());
        public Task<User?> FindByUsernameAsync(string username) => Task.FromResult<User?>(null);
        public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, string? searchTerm = null, int maxResults = 10, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<string, User>>(new Dictionary<string, User>());
        public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;
        public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);
        public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(true);
        public Task CreateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> ListAsync(int skip, int take, string? search, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(new List<User>());
        public Task<int> CountAsync(string? search, CancellationToken ct = default) => Task.FromResult(0);
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    }
    [Fact]
    public async Task AddMemberAsync_OwnerAddsMember_Success()
    {
        var (repo, memberRepo, userRepo, publisher) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo, publisher);
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
        var (repo, memberRepo, userRepo, publisher) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo, publisher);
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
        var (repo, memberRepo, userRepo, publisher) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo, publisher);
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
        var (repo, memberRepo, userRepo, publisher) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo, publisher);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { Id = memberId, ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });

        var result = await handler.RemoveMemberAsync(new RemoveProjectMemberCommand(projectId, memberId, ownerId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.LastOwnerRemovalDenied, result.Error.Code);
    }

    [Fact]
    public async Task AddMemberAsync_AdminNonMember_Succeeds()
    {
        var (repo, memberRepo, userRepo, publisher) = CreateAdminMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo, publisher);
        var projectId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });

        var result = await handler.AddMemberAsync(new AddProjectMemberCommand(projectId, newMemberId, MemberRole.Member, adminId));

        Assert.True(result.IsSuccess);
        Assert.Equal(newMemberId, result.Value.UserId);
        Assert.Equal(MemberRole.Member, result.Value.Role);
    }

    [Fact]
    public async Task UpdateMemberAsync_AdminNonMember_Succeeds()
    {
        var (repo, memberRepo, userRepo, publisher) = CreateAdminMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo, publisher);
        var projectId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { Id = memberId, ProjectId = projectId, UserId = Guid.NewGuid(), Role = MemberRole.Member });

        var result = await handler.UpdateMemberAsync(new UpdateProjectMemberCommand(projectId, memberId, MemberRole.Owner, adminId));

        Assert.True(result.IsSuccess);
        Assert.Equal(MemberRole.Owner, result.Value.Role);
    }

    [Fact]
    public async Task RemoveMemberAsync_AdminNonMember_Succeeds()
    {
        var (repo, memberRepo, userRepo, publisher) = CreateAdminMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo, publisher);
        var projectId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberToRemoveId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { Id = memberToRemoveId, ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = Guid.NewGuid(), Role = MemberRole.Owner });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = Guid.NewGuid(), Role = MemberRole.Member });

        var result = await handler.RemoveMemberAsync(new RemoveProjectMemberCommand(projectId, memberToRemoveId, adminId));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RemoveMemberAsync_NonAdminNonMember_Denies()
    {
        var (repo, memberRepo, userRepo, publisher) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo, publisher);
        var projectId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var memberToRemoveId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { Id = memberToRemoveId, ProjectId = projectId, UserId = Guid.NewGuid(), Role = MemberRole.Owner });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = Guid.NewGuid(), Role = MemberRole.Member });

        var result = await handler.RemoveMemberAsync(new RemoveProjectMemberCommand(projectId, memberToRemoveId, nonMemberId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    private static (
        InMemoryProjectRepository repo,
        InMemoryProjectMemberRepository memberRepo,
        InMemoryUserRepository userRepo,
        FakeProjectBoardEventPublisher publisher
    ) CreateMocks()
    {
        return (
            new InMemoryProjectRepository(),
            new InMemoryProjectMemberRepository(),
            new InMemoryUserRepository(),
            new FakeProjectBoardEventPublisher()
        );
    }

    private static (
        InMemoryProjectRepository repo,
        InMemoryProjectMemberRepository memberRepo,
        FakeUserRepoForAdmin userRepo,
        FakeProjectBoardEventPublisher publisher
    ) CreateAdminMocks()
    {
        return (
            new InMemoryProjectRepository(),
            new InMemoryProjectMemberRepository(),
            new FakeUserRepoForAdmin(),
            new FakeProjectBoardEventPublisher()
        );
    }
}
