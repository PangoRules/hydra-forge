using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.Projects;

public class ProjectMemberServiceTests
{
    [Fact]
    public async Task AddMemberAsync_OwnerAddsMember_Success()
    {
        var (repo, memberRepo, userRepo) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo);
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
        var (repo, memberRepo, userRepo) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo);
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
        var (repo, memberRepo, userRepo) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo);
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
        var (repo, memberRepo, userRepo) = CreateMocks();
        var handler = new ProjectMemberService(repo, memberRepo, userRepo);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project" });
        memberRepo.Members.Add(new ProjectMember { Id = memberId, ProjectId = projectId, UserId = ownerId, Role = MemberRole.Owner });

        var result = await handler.RemoveMemberAsync(new RemoveProjectMemberCommand(projectId, memberId, ownerId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.LastOwnerRemovalDenied, result.Error.Code);
    }

    private static (
        InMemoryProjectRepository repo,
        InMemoryProjectMemberRepository memberRepo,
        InMemoryUserRepository userRepo
    ) CreateMocks()
    {
        return (
            new InMemoryProjectRepository(),
            new InMemoryProjectMemberRepository(),
            new InMemoryUserRepository()
        );
    }
}
