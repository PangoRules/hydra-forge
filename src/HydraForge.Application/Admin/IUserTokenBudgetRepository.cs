namespace HydraForge.Application.Admin;

using HydraForge.Domain.Entities.Admin;

public interface IUserTokenBudgetRepository
{
    Task<UserTokenBudget?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
