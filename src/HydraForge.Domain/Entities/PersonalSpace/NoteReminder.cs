namespace HydraForge.Domain.Entities.PersonalSpace;

public class NoteReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public DateTime TriggerAt { get; set; }
    public string? RepeatPattern { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public bool IsSent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
