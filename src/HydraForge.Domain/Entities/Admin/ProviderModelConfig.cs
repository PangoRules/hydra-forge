using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.Admin;

public class ProviderModelConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProviderId { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ModelTier Tier { get; set; } = ModelTier.Standard;
    public decimal? PricePerToken { get; set; }
    public int? MaxTokens { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
