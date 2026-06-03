namespace HydraForge.Domain.Entities.PersonalSpace;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    // TODO(domain): convert ContentType to a DocumentType enum (pdf/markdown/code/csv/html).
    // Closed domain classification, not a MIME — qualifies for enum under the
    // "string for open external standards, enum for closed classifications" rule.
    public string ContentType { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? Language { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}
