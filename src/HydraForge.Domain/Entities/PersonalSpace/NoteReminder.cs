namespace HydraForge.Domain.Entities.PersonalSpace;

public class NoteReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public DateTime RemindAt { get; set; }
    public bool IsSent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}