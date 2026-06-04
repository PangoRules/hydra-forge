using HydraForge.Domain.Common;

namespace HydraForge.Application.Audit;

/// <summary>
/// Contract for writing audit log entries.
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// Writes an audit log entry.
    /// </summary>
    /// <param name="request">The audit log request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if written, failure with AUDIT_WRITE_FAILED code on error.</returns>
    Task<Result> WriteAsync(AuditLogRequest request, CancellationToken cancellationToken = default);
}