using HydraForge.Application.Admin;
using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Application.Tests.Admin;

public class AdminServiceTests
{
    [Fact]
    public async Task CreateUser_ValidRequest_ReturnsUserDto()
    {
        var repo = new InMemoryAdminUserRepo();
        var hasher = new TestPasswordHasher();
        var service = new AdminService(repo, hasher, new InMemoryAuditLogWriter());
        var request = new CreateUserRequest(
            "newuser",
            "Pass123!",
            "New",
            "User",
            "new@test.com",
            false
        );
        var result = await service.CreateUserAsync(Guid.NewGuid(), request);
        Assert.True(result.IsSuccess);
        Assert.Equal("newuser", result.Value.Username);
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ReturnsFailure()
    {
        var repo = new InMemoryAdminUserRepo();
        await repo.CreateAsync(User.Create("existing", "Existing", "User", "e@test.com", "hash"));
        var hasher = new TestPasswordHasher();
        var service = new AdminService(repo, hasher, new InMemoryAuditLogWriter());
        var request = new CreateUserRequest(
            "existing",
            "Pass123!",
            "New",
            "User",
            "new@test.com",
            false
        );
        var result = await service.CreateUserAsync(Guid.NewGuid(), request);
        Assert.True(result.IsFailure);
        Assert.Equal("USERNAME_TAKEN", result.Error.Code);
    }

    [Fact]
    public async Task DisableUser_NotFound_ReturnsFailure()
    {
        var repo = new InMemoryAdminUserRepo();
        var hasher = new TestPasswordHasher();
        var service = new AdminService(repo, hasher, new InMemoryAuditLogWriter());
        var result = await service.DisableUserAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(result.IsFailure);
        Assert.Equal("USER_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task DisableUser_SelfDisable_ReturnsFailure()
    {
        var repo = new InMemoryAdminUserRepo();
        var user = User.Create("admin", "Admin", "User", "a@test.com", "hash", isAdmin: true);
        await repo.CreateAsync(user);
        var hasher = new TestPasswordHasher();
        var service = new AdminService(repo, hasher, new InMemoryAuditLogWriter());
        var result = await service.DisableUserAsync(user.Id, user.Id);
        Assert.True(result.IsFailure);
        Assert.Equal("SELF_DISABLE", result.Error.Code);
    }

    [Fact]
    public async Task ToggleAdminRole_SelfDemotion_ReturnsFailure()
    {
        var repo = new InMemoryAdminUserRepo();
        var user = User.Create("admin", "Admin", "User", "a@test.com", "hash", isAdmin: true);
        await repo.CreateAsync(user);
        var hasher = new TestPasswordHasher();
        var service = new AdminService(repo, hasher, new InMemoryAuditLogWriter());
        var result = await service.ToggleAdminRoleAsync(user.Id, user.Id);
        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_SELF_DEMOTION", result.Error.Code);
    }

    [Fact]
    public async Task ListUsers_ReturnsPagedResults()
    {
        var repo = new InMemoryAdminUserRepo();
        await repo.CreateAsync(User.Create("alice", "A", "U", "a@test.com", "hash"));
        await repo.CreateAsync(User.Create("bob", "B", "U", "b@test.com", "hash"));
        var hasher = new TestPasswordHasher();
        var service = new AdminService(repo, hasher, new InMemoryAuditLogWriter());
        var result = await service.ListUsersAsync(0, 10, null);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task DisableUser_AdminUser_ReturnsFailure()
    {
        var repo = new InMemoryAdminUserRepo();
        var admin = User.Create("admin", "Admin", "User", "a@test.com", "hash", isAdmin: true);
        await repo.CreateAsync(admin);
        var hasher = new TestPasswordHasher();
        var service = new AdminService(repo, hasher, new InMemoryAuditLogWriter());
        var actorId = Guid.NewGuid();
        var result = await service.DisableUserAsync(actorId, admin.Id);
        Assert.True(result.IsFailure);
        Assert.Equal("CANNOT_DISABLE_ADMIN", result.Error.Code);
    }
}

internal class InMemoryAdminUserRepo : IUserRepository
{
    private readonly List<User> _users = [];

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, User>>(
            _users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id)
        );

    public Task<User?> FindByUsernameAsync(string username) =>
        Task.FromResult(
            _users.FirstOrDefault(u =>
                string.Equals(
                    u.UsernameNormalized,
                    u.Username,
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
        );

    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
        IReadOnlyList<string> usernames,
        string? searchTerm = null,
        int maxResults = 10,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyDictionary<string, User>>(new Dictionary<string, User>());

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(_users.Any(u => u.IsAdmin));

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == userId)?.IsAdmin ?? false);

    public Task CreateAsync(User user, CancellationToken ct = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> ListAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    )
    {
        var query = _users.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
            );
        return Task.FromResult<IReadOnlyList<User>>([
            .. query.OrderBy(u => u.Username).Skip(skip).Take(take),
        ]);
    }

    public Task<int> CountAsync(string? search, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(search))
            return Task.FromResult(_users.Count);
        return Task.FromResult(
            _users.Count(u => u.Username.Contains(search, StringComparison.OrdinalIgnoreCase))
        );
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var idx = _users.FindIndex(u => u.Id == user.Id);
        if (idx >= 0)
            _users[idx] = user;
        return Task.CompletedTask;
    }
}

internal class TestPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => "hashed:" + password;

    public bool VerifyPassword(string password, string hash) => hash == "hashed:" + password;
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
