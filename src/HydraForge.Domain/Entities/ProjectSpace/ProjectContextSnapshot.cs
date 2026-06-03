namespace HydraForge.Domain.Entities.ProjectSpace;

public class ProjectContextSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public string? AiNarrative { get; set; }
    public DateTime TemplateGeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AiNarrativeGeneratedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
