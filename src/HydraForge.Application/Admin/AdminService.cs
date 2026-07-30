using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Admin;

public class AdminService(
    IUserRepository userRepo,
    IPasswordHasher passwordHasher,
    IAuditLogWriter auditLogWriter
) : IAdminService
{
    private sealed record UserAuditSnapshot(
        string Username,
        string Name,
        string Email,
        bool IsAdmin,
        bool IsDisabled
    );

    private static UserAuditSnapshot BuildSnapshot(User user) =>
        new(user.Username, user.Name, user.Email, user.IsAdmin, user.IsDisabled);

    private Task<Result> WriteAuditAsync(
        Guid actorId,
        Guid userId,
        string action,
        string? oldValueJson,
        string? newValueJson,
        CancellationToken ct
    ) =>
        auditLogWriter.WriteAsync(
            new AuditLogRequest(
                actorId,
                AuditLogScope.System,
                "User",
                userId,
                action,
                null,
                oldValueJson,
                newValueJson
            ),
            ct
        );

    public async Task<Result<UserListPageDto>> ListUsersAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    )
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);
        var users = await userRepo.ListAsync(skip, take, search, ct);
        var totalCount = await userRepo.CountAsync(search, ct);
        return Result<UserListPageDto>.Success(
            new UserListPageDto([.. users.Select(MapToDto)], totalCount)
        );
    }

    public async Task<Result<UserDto>> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepo.FindByIdAsync(userId, ct);
        if (user == null)
            return Result<UserDto>.Failure(new Error("USER_NOT_FOUND", "User not found."));
        return Result<UserDto>.Success(MapToDto(user));
    }

    public async Task<Result<UserDto>> CreateUserAsync(
        Guid actorId,
        CreateUserRequest request,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Result<UserDto>.Failure(new Error("VALIDATION_ERROR", "Username is required."));
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return Result<UserDto>.Failure(
                new Error("VALIDATION_ERROR", "Password must be at least 6 characters.")
            );

        var existing = await userRepo.FindByUsernameAsync(request.Username);
        if (existing != null)
            return Result<UserDto>.Failure(new Error("USERNAME_TAKEN", "Username already exists."));

        var user = User.Create(
            request.Username,
            request.Name,
            request.LastName,
            request.Email,
            passwordHasher.HashPassword(request.Password),
            request.IsAdmin
        );

        await userRepo.CreateAsync(user, ct);
        await WriteAuditAsync(
            actorId,
            user.Id,
            "Created",
            null,
            AuditSnapshot.Serialize(BuildSnapshot(user)),
            ct
        );
        return Result<UserDto>.Success(MapToDto(user));
    }

    public async Task<Result> DisableUserAsync(
        Guid actorId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        if (actorId == userId)
            return Result.Failure(new Error("SELF_DISABLE", "Cannot disable your own account."));

        var user = await userRepo.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure(new Error("USER_NOT_FOUND", "User not found."));

        if (user.IsAdmin)
            return Result.Failure(
                new Error(
                    "CANNOT_DISABLE_ADMIN",
                    "Cannot disable an admin user. Use ToggleAdminRole first."
                )
            );

        var oldSnapshot = BuildSnapshot(user);
        user.Disable();
        await userRepo.UpdateAsync(user, ct);
        await WriteAuditAsync(
            actorId,
            user.Id,
            "Disabled",
            AuditSnapshot.Serialize(oldSnapshot),
            AuditSnapshot.Serialize(BuildSnapshot(user)),
            ct
        );
        return Result.Success();
    }

    public async Task<Result> EnableUserAsync(
        Guid actorId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await userRepo.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure(new Error("USER_NOT_FOUND", "User not found."));
        var oldSnapshot = BuildSnapshot(user);
        user.Enable();
        await userRepo.UpdateAsync(user, ct);
        await WriteAuditAsync(
            actorId,
            user.Id,
            "Enabled",
            AuditSnapshot.Serialize(oldSnapshot),
            AuditSnapshot.Serialize(BuildSnapshot(user)),
            ct
        );
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        Guid actorId,
        Guid userId,
        string newPassword,
        CancellationToken ct = default
    )
    {
        var user = await userRepo.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure(new Error("USER_NOT_FOUND", "User not found."));
        user.SetPasswordHash(passwordHasher.HashPassword(newPassword));
        await userRepo.UpdateAsync(user, ct);
        // Password hash is never included in the snapshot — the audit entry records
        // that a reset happened and by whom, not the secret value.
        await WriteAuditAsync(
            actorId,
            user.Id,
            "PasswordReset",
            null,
            AuditSnapshot.Serialize(new { ResetAt = DateTime.UtcNow }),
            ct
        );
        return Result.Success();
    }

    public async Task<Result> ToggleAdminRoleAsync(
        Guid actorId,
        Guid targetUserId,
        CancellationToken ct = default
    )
    {
        if (actorId == targetUserId)
            return Result.Failure(
                new Error("ADMIN_SELF_DEMOTION", "Cannot remove your own admin role.")
            );

        var user = await userRepo.FindByIdAsync(targetUserId, ct);
        if (user == null)
            return Result.Failure(new Error("USER_NOT_FOUND", "User not found."));
        var oldSnapshot = BuildSnapshot(user);
        user.SetAdminRole(!user.IsAdmin);
        await userRepo.UpdateAsync(user, ct);
        await WriteAuditAsync(
            actorId,
            user.Id,
            "RoleChanged",
            AuditSnapshot.Serialize(oldSnapshot),
            AuditSnapshot.Serialize(BuildSnapshot(user)),
            ct
        );
        return Result.Success();
    }

    private static UserDto MapToDto(User u) =>
        new(u.Id, u.Username, u.Name, u.Email, u.IsAdmin, u.IsDisabled, u.LastLoginAt, u.CreatedAt);
}
