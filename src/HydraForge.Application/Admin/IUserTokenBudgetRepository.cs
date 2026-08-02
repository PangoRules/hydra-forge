namespace HydraForge.Application.Admin;

using HydraForge.Domain.Entities.Admin;

public interface IUserTokenBudgetRepository
{
    Task<UserTokenBudget?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpdateAsync(UserTokenBudget budget, CancellationToken ct = default);
}
