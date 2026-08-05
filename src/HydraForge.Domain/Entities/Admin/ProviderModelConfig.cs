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
    public bool SupportsReasoning { get; set; } = false;
    public OllamaThinkMode OllamaThinkMode { get; set; } = OllamaThinkMode.Auto;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void UpdateSupportsReasoning(bool supportsReasoning)
    {
        SupportsReasoning = supportsReasoning;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateOllamaThinkMode(OllamaThinkMode mode)
    {
        OllamaThinkMode = mode;
        UpdatedAt = DateTime.UtcNow;
    }
}
