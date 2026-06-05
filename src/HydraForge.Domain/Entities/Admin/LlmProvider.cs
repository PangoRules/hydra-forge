using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.Admin;

public class LlmProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public AdapterType AdapterType { get; set; }
    public ProviderType ProviderType { get; set; }
    public ModelTier Tier { get; set; } = ModelTier.Standard;
    public Guid? FallbackProviderId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
