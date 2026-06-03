using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Application.Auth;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username);
    Task UpdateLastLoginAsync(Guid userId, DateTime loginAt);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public interface IAccessTokenIssuer
{
    string IssueToken(User user);
}