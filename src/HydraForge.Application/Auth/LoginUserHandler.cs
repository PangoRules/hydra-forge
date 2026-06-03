using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Application.Auth;

public class LoginUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _accessTokenIssuer;

    public LoginUserHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, IAccessTokenIssuer accessTokenIssuer)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _accessTokenIssuer = accessTokenIssuer;
    }

    public async Task<Result<LoginResponse>> HandleAsync(LoginRequest request)
    {
        var user = await _userRepository.FindByUsernameAsync(request.Username);
        if (user == null)
        {
            return Result<LoginResponse>.Failure(new Error(DomainErrorCodes.Auth.InvalidCredentials, "Invalid credentials."));
        }

        if (user.IsDisabled)
        {
            return Result<LoginResponse>.Failure(new Error(DomainErrorCodes.Auth.UserDisabled, "User account is disabled."));
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result<LoginResponse>.Failure(new Error(DomainErrorCodes.Auth.InvalidCredentials, "Invalid credentials."));
        }

        var token = _accessTokenIssuer.IssueToken(user);
        await _userRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);

        return Result<LoginResponse>.Success(new LoginResponse(token, DateTimeOffset.UtcNow.AddHours(1), user.Id, user.Username, user.IsAdmin));
    }
}