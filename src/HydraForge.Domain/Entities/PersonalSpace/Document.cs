namespace HydraForge.Domain.Entities.PersonalSpace;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? Language { get; set; }
    public bool IsArchived { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
