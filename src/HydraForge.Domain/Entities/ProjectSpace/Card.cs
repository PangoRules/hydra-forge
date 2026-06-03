using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid ColumnId { get; set; }
    public Guid? ParentCardId { get; set; }
    public Guid? SpecId { get; set; }
    public Guid? PlanId { get; set; }
    public int CardNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CardType Type { get; set; } = CardType.Task;
    public int Position { get; set; }
    public DateTime? DueAt { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime MovedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}
