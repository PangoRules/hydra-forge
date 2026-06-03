namespace HydraForge.Domain.Entities.PersonalSpace;

public class SystemSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ArchivedItemRetentionDays { get; set; } = 730;
    public int AuditLogRetentionDays { get; set; } = 90;
    public int NotificationRetentionDays { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
