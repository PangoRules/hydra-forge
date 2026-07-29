namespace HydraForge.Server.Tests.Auth;

using System.Security.Claims;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Server.Auth;
using Xunit;

public class ClaimsPrincipalExtensionsTests
{
    private sealed class StubMemberRepo : IProjectMemberRepository
    {
        public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<ProjectMember?> GetByProjectAndUserAsync(
            Guid projectId,
            Guid userId,
            CancellationToken ct = default
        ) => Task.FromResult<ProjectMember?>(null);

        public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
            Guid projectId,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
            IEnumerable<Guid> projectIds,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
            IEnumerable<Guid> projectIds,
            Guid userId,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class StubMemberRepoWithMembership : IProjectMemberRepository
    {
        public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<ProjectMember?> GetByProjectAndUserAsync(
            Guid projectId,
            Guid userId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<ProjectMember?>(
                new ProjectMember { ProjectId = projectId, UserId = userId }
            );

        public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
            Guid projectId,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
            IEnumerable<Guid> projectIds,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
            IEnumerable<Guid> projectIds,
            Guid userId,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, bool isAdmin)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "testuser"),
        };
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task IsProjectMemberOrAdmin_AdminRole_ReturnsTrueWithoutCheckingMembership()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var principal = CreatePrincipal(userId, isAdmin: true);
        var stubRepo = new StubMemberRepo();

        var result = await principal.IsProjectMemberOrAdmin(stubRepo, projectId);

        Assert.True(result);
    }

    [Fact]
    public async Task IsProjectMemberOrAdmin_NonAdminWithMembership_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var principal = CreatePrincipal(userId, isAdmin: false);
        var stubRepo = new StubMemberRepoWithMembership();

        var result = await principal.IsProjectMemberOrAdmin(stubRepo, projectId);

        Assert.True(result);
    }

    [Fact]
    public async Task IsProjectMemberOrAdmin_NonAdminWithoutMembership_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var principal = CreatePrincipal(userId, isAdmin: false);
        var stubRepo = new StubMemberRepo();

        var result = await principal.IsProjectMemberOrAdmin(stubRepo, projectId);

        Assert.False(result);
    }
}
