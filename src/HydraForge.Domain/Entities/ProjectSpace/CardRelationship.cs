using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class CardRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceCardId { get; set; }
    public Guid TargetCardId { get; set; }
    public RelationshipType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public DateTime? ArchivedAt { get; set; }
}
