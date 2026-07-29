using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Xunit;

namespace HydraForge.Application.Tests.Auth;

public class MembershipGuardTests
{
    private sealed class FakeUserRepo(bool isAdmin) : IUserRepository
    {
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<User?> FindByUsernameAsync(string username)
            => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, string? searchTerm = null, int maxResults = 10, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt)
            => throw new NotImplementedException();
        public Task<bool> AnyAdminExistsAsync()
            => throw new NotImplementedException();
        public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(isAdmin);
        public Task CreateAsync(User user)
            => throw new NotImplementedException();
    }

    private sealed class FakeMemberRepo(bool hasMembership) : IProjectMemberRepository
    {
        public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(hasMembership ? new ProjectMember() : null);
        public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(IEnumerable<Guid> projectIds, Guid userId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task HasAccessAsync_Admin_ReturnsTrueRegardlessOfMembership()
    {
        var result = await MembershipGuard.HasAccessAsync(
            new FakeUserRepo(isAdmin: true),
            new FakeMemberRepo(hasMembership: false),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task HasAccessAsync_NonAdminMember_ReturnsTrue()
    {
        var result = await MembershipGuard.HasAccessAsync(
            new FakeUserRepo(isAdmin: false),
            new FakeMemberRepo(hasMembership: true),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task HasAccessAsync_NonAdminNonMember_ReturnsFalse()
    {
        var result = await MembershipGuard.HasAccessAsync(
            new FakeUserRepo(isAdmin: false),
            new FakeMemberRepo(hasMembership: false),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.False(result);
    }
}
