namespace HydraForge.Application.Audit;

public record AuditLogQuery(
    Guid? ProjectId = null,
    Guid? ActorId = null,
    string? EntityType = null,
    string? Action = null,
    DateTime? From = null,
    DateTime? To = null,
    int Skip = 0,
    int Take = 50
);

public record AuditLogQueryResult(IReadOnlyList<AuditLogEntryDto> Items, int TotalCount);

public record AuditLogEntryDto(
    Guid Id,
    Guid? ProjectId,
    string? ProjectName,
    Guid ActorId,
    string ActorName,
    string EntityType,
    Guid EntityId,
    string Action,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp,
    string Scope
);
