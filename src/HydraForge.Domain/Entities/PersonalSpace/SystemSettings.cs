namespace HydraForge.Domain.Entities.PersonalSpace;

public class SystemSettings
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = Guid.NewGuid();
    public int ArchivedItemRetentionDays { get; set; } = 730;
    public int AuditLogRetentionDays { get; set; } = 90;
    public int NotificationRetentionDays { get; set; } = 30;
    public string? NtfyServerUrl { get; set; }
    public string? SearXngUrl { get; set; }
    public string? BrandName { get; set; }
    public string? BrandLogoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void UpdateSettings(
        int? archivedItemRetentionDays = null,
        int? auditLogRetentionDays = null,
        int? notificationRetentionDays = null,
        string? ntfyServerUrl = null,
        string? searXngUrl = null,
        string? brandName = null,
        string? brandLogoUrl = null
    )
    {
        if (archivedItemRetentionDays.HasValue)
            ArchivedItemRetentionDays = archivedItemRetentionDays.Value;
        if (auditLogRetentionDays.HasValue)
            AuditLogRetentionDays = auditLogRetentionDays.Value;
        if (notificationRetentionDays.HasValue)
            NotificationRetentionDays = notificationRetentionDays.Value;
        if (ntfyServerUrl is not null)
            NtfyServerUrl = ntfyServerUrl;
        if (searXngUrl is not null)
            SearXngUrl = searXngUrl;
        if (brandName is not null)
            BrandName = brandName;
        if (brandLogoUrl is not null)
            BrandLogoUrl = brandLogoUrl;
        UpdatedAt = DateTime.UtcNow;
    }
}
