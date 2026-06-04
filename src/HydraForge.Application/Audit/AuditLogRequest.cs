namespace HydraForge.Application.Audit;

using HydraForge.Domain.Enums;

/// <summary>
/// Request to write an audit log entry.
/// </summary>
/// <param name="ActorId">User who performed the action.</param>
/// <param name="Scope">Audit scope: Project, System, or Personal.</param>
/// <param name="EntityType">Type of entity being audited (e.g., "Card", "Document").</param>
/// <param name="EntityId">ID of the entity being audited.</param>
/// <param name="Action">Action performed (e.g., "Created", "Updated", "Deleted").</param>
/// <param name="ProjectId">Project scope of the action (required for Project scope, null for System/Personal).</param>
/// <param name="OldValueJson">JSON representation of the old state (null for creates).</param>
/// <param name="NewValueJson">JSON representation of the new state (null for deletes).</param>
public record AuditLogRequest(
    Guid ActorId,
    AuditLogScope Scope,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid? ProjectId = null,
    string? OldValueJson = null,
    string? NewValueJson = null
);