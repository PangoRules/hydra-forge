namespace HydraForge.Domain.Entities.Admin;

public class ImageUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public int ImageCount { get; set; }
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}