namespace HydraForge.Domain.Entities.ProjectSpace;

public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public Guid ActorId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}