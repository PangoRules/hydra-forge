using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.Admin;

public class TokenUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public AiFeature Feature { get; set; }
    public Guid ProviderId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokens { get; set; }
    public Guid? PipelineRunId { get; set; }
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
