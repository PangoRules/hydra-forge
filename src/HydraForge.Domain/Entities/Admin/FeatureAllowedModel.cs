namespace HydraForge.Domain.Entities.Admin;

public class FeatureAllowedModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FeatureRoutingConfigId { get; set; }
    public Guid ProviderModelConfigId { get; set; }
    public int Priority { get; set; }
}
