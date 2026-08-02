namespace HydraForge.Domain.Entities.Admin;

using HydraForge.Domain.Enums;

public class FeatureRoutingConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AiFeature Feature { get; set; }
    public ModelTier DefaultTier { get; set; } = ModelTier.Standard;
    public ModelTier? MaxUserTier { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
