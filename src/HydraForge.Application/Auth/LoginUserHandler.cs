using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Application.Auth;

// ── Ports / contracts ──────────────────────────────────────

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    Task<User?> FindByUsernameAsync(string username);
    Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, CancellationToken ct = default);
    Task<List<User>> SearchByUsernameAsync(string query, int maxResults = 10, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid userId, DateTime loginAt);
    Task<bool> AnyAdminExistsAsync();
    Task CreateAsync(User user);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public interface IAccessTokenIssuer
{
    AccessToken IssueToken(User user);
}

// ── DTOs ───────────────────────────────────────────────────

public record LoginRequest(string Username, string Password);

public record AccessToken(string Value, DateTimeOffset ExpiresAt);

public record RefreshTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

public record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Username,
    bool IsAdmin
);

// ── Handler / use-case ────────────────────────────────────

public class LoginUserHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer
)
{
    public async Task<Result<LoginResponse>> HandleAsync(LoginRequest request)
    {
        var user = await userRepository.FindByUsernameAsync(request.Username);
        if (user == null)
        {
            return Result<LoginResponse>.Failure(
                new Error(DomainErrorCodes.Auth.InvalidCredentials, "Invalid credentials.")
            );
        }

        if (user.IsDisabled)
        {
            return Result<LoginResponse>.Failure(
                new Error(DomainErrorCodes.Auth.UserDisabled, "User account is disabled.")
            );
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result<LoginResponse>.Failure(
                new Error(DomainErrorCodes.Auth.InvalidCredentials, "Invalid credentials.")
            );
        }

        var token = accessTokenIssuer.IssueToken(user);
        await userRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);

        return Result<LoginResponse>.Success(
            new LoginResponse(
                token.Value,
                token.ExpiresAt,
                user.Id,
                user.Username,
                user.IsAdmin
            )
        );
    }
}
