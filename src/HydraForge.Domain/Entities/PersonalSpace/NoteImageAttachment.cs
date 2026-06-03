namespace HydraForge.Domain.Entities.PersonalSpace;

public class NoteImageAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
