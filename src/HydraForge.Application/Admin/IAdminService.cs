using HydraForge.Domain.Common;

namespace HydraForge.Application.Admin;

public interface IAdminService
{
    Task<Result<UserListPageDto>> ListUsersAsync(int skip, int take, string? search, CancellationToken ct = default);
    Task<Result<UserDto>> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result> DisableUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> EnableUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);
    Task<Result> ToggleAdminRoleAsync(Guid actorId, Guid targetUserId, CancellationToken ct = default);
}

public record CreateUserRequest(string Username, string Password, string Name, string LastName, string Email, bool IsAdmin);
public record UserDto(Guid Id, string Username, string Name, string Email, bool IsAdmin, bool IsDisabled, DateTime? LastLoginAt, DateTime CreatedAt);
public record UserListPageDto(IReadOnlyList<UserDto> Items, int TotalCount);
