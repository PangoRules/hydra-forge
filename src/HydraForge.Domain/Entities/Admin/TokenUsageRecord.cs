namespace HydraForge.Domain.Entities.Admin;

public class TokenUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid ModelConfigId { get; set; }
    public int TokenCount { get; set; }
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}