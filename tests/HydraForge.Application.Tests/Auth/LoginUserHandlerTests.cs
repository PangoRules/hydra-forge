using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Application.Tests.Auth;

public class LoginUserHandlerTests
{
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccessWithToken()
    {
        var user = User.Create("admin", "Test", "User", "test@localhost", "hashed", isAdmin: true);
        var repo = new InMemoryUserRepository(user);
        var hasher = new StrictPasswordHasher(true);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var issuer = new FixedTokenIssuer("jwt-token", expiresAt);

        var handler = new LoginUserHandler(repo, hasher, issuer);
        var request = new LoginRequest("admin", "password123");

        var result = await handler.HandleAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("jwt-token", result.Value.AccessToken);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("admin", result.Value.Username);
        Assert.True(result.Value.IsAdmin);
        Assert.Equal(expiresAt, result.Value.ExpiresAt);
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentialsError()
    {
        var user = User.Create("admin", "Test", "User", "test@localhost", "hashed", isAdmin: true);
        var repo = new InMemoryUserRepository(user);
        var hasher = new StrictPasswordHasher(false);
        var issuer = new FixedTokenIssuer("jwt-token");

        var handler = new LoginUserHandler(repo, hasher, issuer);
        var request = new LoginRequest("admin", "wrongpassword");

        var result = await handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Auth.InvalidCredentials, result.Error.Code);
    }

    [Fact]
    public async Task Handle_DisabledUser_ReturnsUserDisabledError()
    {
        var user = User.Create("admin", "Test", "User", "test@localhost", "hashed", isAdmin: true);
        user.Disable();
        var repo = new InMemoryUserRepository(user);
        var hasher = new StrictPasswordHasher(true);
        var issuer = new FixedTokenIssuer("jwt-token");

        var handler = new LoginUserHandler(repo, hasher, issuer);
        var request = new LoginRequest("admin", "password123");

        var result = await handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Auth.UserDisabled, result.Error.Code);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsInvalidCredentialsError()
    {
        var repo = new InMemoryUserRepository(null);
        var hasher = new StrictPasswordHasher(true);
        var issuer = new FixedTokenIssuer("jwt-token");

        var handler = new LoginUserHandler(repo, hasher, issuer);
        var request = new LoginRequest("nonexistent", "password123");

        var result = await handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Auth.InvalidCredentials, result.Error.Code);
    }
}

internal class InMemoryUserRepository(User? user) : IUserRepository
{
    private readonly User? _user = user;

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_user);

    public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, User>>(
            _user != null && ids.Contains(_user.Id)
                ? new Dictionary<Guid, User> { [_user.Id] = _user }
                : []
        );

    public Task<User?> FindByUsernameAsync(string username) => Task.FromResult(_user);

    public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
        IReadOnlyList<string> usernames,
        string? searchTerm = null,
        int maxResults = 10,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<string, User>>(
            _user != null && usernames.Contains(_user.Username, StringComparer.OrdinalIgnoreCase)
                ? new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase)
                {
                    [_user.Username] = _user,
                }
                : new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase)
        );

    public static Task<List<User>> SearchByUsernameAsync() => Task.FromResult(new List<User>());

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync() => Task.FromResult(_user?.IsAdmin ?? false);

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_user?.IsAdmin ?? false);

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

internal class StrictPasswordHasher(bool verifyResult) : IPasswordHasher
{
    private readonly bool _verifyResult = verifyResult;

    public string HashPassword(string password) => "hashed";

    public bool VerifyPassword(string password, string hash) => _verifyResult;
}

internal class FixedTokenIssuer : IAccessTokenIssuer
{
    private readonly string _token;
    private readonly DateTimeOffset _expiresAt;

    public FixedTokenIssuer(string token)
    {
        _token = token;
        _expiresAt = DateTimeOffset.UtcNow.AddHours(1);
    }

    public FixedTokenIssuer(string token, DateTimeOffset expiresAt)
    {
        _token = token;
        _expiresAt = expiresAt;
    }

    public AccessToken IssueToken(User user) => new(_token, _expiresAt);
}
