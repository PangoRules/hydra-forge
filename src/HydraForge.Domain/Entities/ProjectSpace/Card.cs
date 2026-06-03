using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid ColumnId { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CardType CardType { get; set; } = CardType.Task;
    public int SortOrder { get; set; }
    public DateTime? DueDate { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}