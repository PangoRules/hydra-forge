using HydraForge.Application.Auth;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Auth;

public class EfUserRepository(HydraForgeDbContext context) : IUserRepository
{
    public async Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> FindByUsernameAsync(string username)
    {
        var normalized = username.ToLowerInvariant();
        return await context.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == normalized);
    }

    public async Task UpdateLastLoginAsync(Guid userId, DateTime loginAt)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null)
            return;
        user.LastLoginAt = loginAt;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task<bool> AnyAdminExistsAsync()
    {
        return await context.Users.AnyAsync(u => u.IsAdmin);
    }

    public async Task CreateAsync(User user)
    {
        user.UsernameNormalized = user.Username.ToLowerInvariant();
        user.EmailNormalized = user.Email.ToLowerInvariant();
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        context.Users.Add(user);
        await context.SaveChangesAsync();
    }
}
