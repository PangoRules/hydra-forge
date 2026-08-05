namespace HydraForge.Domain.Entities.ProjectSpace;

public class ProjectDocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectDocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
}
