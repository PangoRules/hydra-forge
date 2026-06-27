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

    public async Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        return await context.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
    }

    public async Task<User?> FindByUsernameAsync(string username)
    {
        var normalized = username.ToLowerInvariant();
        return await context.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == normalized);
    }

    public async Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, CancellationToken ct = default)
    {
        var normalized = usernames.Select(u => u.ToLowerInvariant()).ToList();
        var users = await context.Users.Where(u => normalized.Contains(u.UsernameNormalized)).ToListAsync(ct);
        return users.ToDictionary(u => u.Username, u => u, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<User>> SearchByUsernameAsync(string query, int maxResults = 10, CancellationToken ct = default)
    {
        var normalized = query.ToLowerInvariant();
        return await context.Users
            .Where(u => EF.Functions.ILike(u.Username, $"%{normalized}%"))
            .Take(maxResults)
            .ToListAsync(ct);
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
