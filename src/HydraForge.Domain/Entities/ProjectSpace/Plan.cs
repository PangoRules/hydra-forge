using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid CardId { get; set; }
    public Guid? SpecId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public PlanStatus Status { get; set; } = PlanStatus.Pending;
    public int Position { get; set; } = 0;
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDone => Status == PlanStatus.Done;

    public void Activate() => Status = PlanStatus.Active;

    public void Complete() => Status = PlanStatus.Done;

    public void Reactivate() => Status = PlanStatus.Active;
}
