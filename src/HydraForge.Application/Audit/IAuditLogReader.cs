namespace HydraForge.Application.Audit;

public interface IAuditLogReader
{
    Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}
