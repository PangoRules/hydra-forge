namespace HydraForge.Domain.Entities.PersonalSpace;

public class CalendarSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CalDavUrl { get; set; }
    public string? CalDavUsername { get; set; }
    public string? CalDavPasswordEncrypted { get; set; }
    public string Color { get; set; } = string.Empty;
    public DateTime? LastSyncAt { get; set; }
    public string? ExternalUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}
