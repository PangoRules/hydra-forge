using HydraForge.Application.Auth;
using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Server.Tests;

internal class FakeUserRepository : IUserRepository
{
    private static User MakeUser(string username, string email) =>
        User.Create(username, "Test", "User", email, "hash");

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<User?>(MakeUser("TestUser", "test@test.com"));

    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyDictionary<Guid, User>>(new Dictionary<Guid, User>());

    public Task<User?> FindByUsernameAsync(string username) =>
        Task.FromResult<User?>(MakeUser("TestUser", "test@test.com"));

    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
        IReadOnlyList<string> usernames,
        string? searchTerm = null,
        int maxResults = 10,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyDictionary<string, User>>(new Dictionary<string, User>());

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(true);

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task CreateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<User>> ListAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<User>>([]);

    public Task<int> CountAsync(string? search, CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
}
