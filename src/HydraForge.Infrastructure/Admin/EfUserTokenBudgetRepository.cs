namespace HydraForge.Infrastructure.Admin;

using HydraForge.Application.Admin;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfUserTokenBudgetRepository : IUserTokenBudgetRepository
{
    private readonly HydraForgeDbContext _db;

    public EfUserTokenBudgetRepository(HydraForgeDbContext db)
    {
        _db = db;
    }

    public async Task<UserTokenBudget?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.UserTokenBudgets.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == userId, ct);
    }
}
