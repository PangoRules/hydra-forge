namespace HydraForge.Domain.Entities.ProjectSpace;

using HydraForge.Domain.Enums;

public class AuditLogEntry
{
    public Guid Id { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid ActorId { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime Timestamp { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public AuditLogScope Scope { get; private set; }

    private AuditLogEntry() { }

    public static AuditLogEntry Create(
        Guid actorId,
        AuditLogScope scope,
        string entityType,
        Guid entityId,
        string action,
        Guid? projectId = null,
        string? oldValue = null,
        string? newValue = null
    )
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("ActorId is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType is required.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));

        // Scope validation
        if (scope == AuditLogScope.Project && projectId == null)
            throw new ArgumentException("ProjectId is required for Project scope.", nameof(projectId));
        if (scope != AuditLogScope.Project && projectId != null)
            throw new ArgumentException("ProjectId must be null for System or Personal scope.", nameof(projectId));

        var now = DateTime.UtcNow;

        return new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Scope = scope,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ProjectId = projectId,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = now,
            CreatedAt = now
        };
    }
}