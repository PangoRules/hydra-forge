namespace HydraForge.Domain.Entities.ProjectSpace;

public class SpecVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpecId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
}
