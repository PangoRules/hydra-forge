namespace HydraForge.Domain.Entities.ProjectSpace;

public class ProjectContextSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}