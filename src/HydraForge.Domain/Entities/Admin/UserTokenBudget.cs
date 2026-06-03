namespace HydraForge.Domain.Entities.Admin;

public class UserTokenBudget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int MonthlyTokenBudget { get; set; }
    public int MonthlyTokenUsed { get; set; }
    public int MonthlyImageBudget { get; set; }
    public int MonthlyImageUsed { get; set; }
    public DateTime PeriodStart { get; set; } = DateTime.UtcNow;
    public DateTime PeriodEnd { get; set; } = DateTime.UtcNow;
}