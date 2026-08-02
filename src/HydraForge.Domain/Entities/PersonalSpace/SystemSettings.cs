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
    public TimeSpan? AiNarrativeGenerationTimeUtc { get; set; } = TimeSpan.Zero;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void UpdateSettings(
        int? archivedItemRetentionDays = null,
        int? auditLogRetentionDays = null,
        int? notificationRetentionDays = null,
        string? ntfyServerUrl = null,
        string? searXngUrl = null,
        string? brandName = null,
        string? brandLogoUrl = null,
        TimeSpan? aiNarrativeGenerationTimeUtc = null
    )
    {
        if (archivedItemRetentionDays.HasValue)
            ArchivedItemRetentionDays = archivedItemRetentionDays.Value;
        if (auditLogRetentionDays.HasValue)
            AuditLogRetentionDays = auditLogRetentionDays.Value;
        if (notificationRetentionDays.HasValue)
            NotificationRetentionDays = notificationRetentionDays.Value;
        if (ntfyServerUrl is not null)
            NtfyServerUrl = string.IsNullOrWhiteSpace(ntfyServerUrl) ? null : ntfyServerUrl;
        if (searXngUrl is not null)
            SearXngUrl = string.IsNullOrWhiteSpace(searXngUrl) ? null : searXngUrl;
        if (brandName is not null)
            BrandName = string.IsNullOrWhiteSpace(brandName) ? null : brandName;
        if (brandLogoUrl is not null)
            BrandLogoUrl = string.IsNullOrWhiteSpace(brandLogoUrl) ? null : brandLogoUrl;
        if (aiNarrativeGenerationTimeUtc.HasValue)
            AiNarrativeGenerationTimeUtc = aiNarrativeGenerationTimeUtc.Value;
        UpdatedAt = DateTime.UtcNow;
    }
}
