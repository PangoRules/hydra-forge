using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.Admin;

public class ImageUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public AiFeature Feature { get; set; }
    public Guid ProviderModelConfigId { get; set; }
    public Guid ProviderId { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int ImageCount { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
