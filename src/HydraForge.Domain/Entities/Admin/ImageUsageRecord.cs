namespace HydraForge.Domain.Entities.Admin;

public class ImageUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Feature { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int ImageCount { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}