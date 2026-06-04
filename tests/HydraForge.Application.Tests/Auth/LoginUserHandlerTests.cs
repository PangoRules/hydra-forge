using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Application.Tests.Auth;

public class LoginUserHandlerTests
{
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccessWithToken()
    {
        var user = new TestUser { Id = Guid.NewGuid(), Username = "admin", PasswordHash = "hashed", IsAdmin = true, IsDisabled = false };
        var repo = new InMemoryUserRepository(user);
        var hasher = new StrictPasswordHasher(true);
        var issuer = new FixedTokenIssuer("jwt-token");

        var handler = new LoginUserHandler(repo, hasher, issuer);
        var request = new LoginRequest("admin", "password123");

        var result = await handler.HandleAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("jwt-token", result.Value.AccessToken);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("admin", result.Value.Username);
        Assert.True(result.Value.IsAdmin);
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentialsError()
    {
        var user = new TestUser { Id = Guid.NewGuid(), Username = "admin", PasswordHash = "hashed", IsAdmin = true, IsDisabled = false };
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
        var user = new TestUser { Id = Guid.NewGuid(), Username = "admin", PasswordHash = "hashed", IsAdmin = true, IsDisabled = true };
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

internal class TestUser : User
{
}

internal class InMemoryUserRepository : IUserRepository
{
    private readonly User? _user;

    public InMemoryUserRepository(User? user) => _user = user;

    public Task<User?> FindByUsernameAsync(string username)
        => Task.FromResult(_user);

    public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt)
        => Task.CompletedTask;

    public Task<bool> AnyAdminExistsAsync()
        => Task.FromResult(_user?.IsAdmin ?? false);

    public Task CreateAsync(User user)
        => Task.CompletedTask;
}

internal class StrictPasswordHasher : IPasswordHasher
{
    private readonly bool _verifyResult;

    public StrictPasswordHasher(bool verifyResult) => _verifyResult = verifyResult;

    public string HashPassword(string password) => "hashed";

    public bool VerifyPassword(string password, string hash) => _verifyResult;
}

internal class FixedTokenIssuer : IAccessTokenIssuer
{
    private readonly string _token;

    public FixedTokenIssuer(string token) => _token = token;

    public string IssueToken(User user) => _token;
}