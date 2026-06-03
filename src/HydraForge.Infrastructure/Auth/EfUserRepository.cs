using HydraForge.Application.Auth;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Auth;

public class EfUserRepository : IUserRepository
{
    private readonly HydraForgeDbContext _context;

    public EfUserRepository(HydraForgeDbContext context)
    {
        _context = context;
    }

    public async Task<User?> FindByUsernameAsync(string username)
    {
        var normalized = username.ToLowerInvariant();
        return await _context.Users
            .FirstOrDefaultAsync(u => u.UsernameNormalized == normalized);
    }

    public async Task UpdateLastLoginAsync(Guid userId, DateTime loginAt)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        user.LastLoginAt = loginAt;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> AnyAdminExistsAsync()
    {
        return await _context.Users.AnyAsync(u => u.IsAdmin);
    }

    public async Task CreateAsync(User user)
    {
        user.UsernameNormalized = user.Username.ToLowerInvariant();
        user.EmailNormalized = user.Email.ToLowerInvariant();
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }
}