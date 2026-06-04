namespace HydraForge.Application.Audit;

/// <summary>
/// Request to write an audit log entry.
/// </summary>
/// <param name="ActorId">User who performed the action.</param>
/// <param name="ProjectId">Project scope of the action (null for system-level).</param>
/// <param name="EntityType">Type of entity being audited (e.g., "Card", "Document").</param>
/// <param name="EntityId">ID of the entity being audited.</param>
/// <param name="Action">Action performed (e.g., "Created", "Updated", "Deleted").</param>
/// <param name="OldValueJson">JSON representation of the old state (null for creates).</param>
/// <param name="NewValueJson">JSON representation of the new state (null for deletes).</param>
public record AuditLogRequest(
    Guid ActorId,
    Guid? ProjectId,
    string EntityType,
    Guid EntityId,
    string Action,
    string? OldValueJson,
    string? NewValueJson
);