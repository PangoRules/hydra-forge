namespace HydraForge.Domain.Entities.PersonalSpace;

public class CalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CalendarSourceId { get; set; }
    public string? ExternalUid { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsAllDay { get; set; }
    public string? Description { get; set; }
    public string? RecurrenceRule { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}
