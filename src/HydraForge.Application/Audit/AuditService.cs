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

/// <summary>
/// Service that validates and writes audit log entries.
/// </summary>
public class AuditService(IAuditLogWriter writer)
{
    private readonly IAuditLogWriter _writer =
        writer ?? throw new ArgumentNullException(nameof(writer));

    /// <summary>
    /// Validates the request and writes the audit log entry.
    /// </summary>
    /// <param name="request">The audit log request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if validation passes and write succeeds; failure otherwise.</returns>
    public async Task<Result> WriteAsync(
        AuditLogRequest request,
        CancellationToken cancellationToken = default
    )
    {
        // Validate required fields
        if (request.ActorId == Guid.Empty)
            return Result.Failure(
                new Error(DomainErrorCodes.Infrastructure.AuditWriteFailed, "ActorId is required.")
            );

        if (string.IsNullOrWhiteSpace(request.EntityType))
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Infrastructure.AuditWriteFailed,
                    "EntityType is required."
                )
            );

        if (string.IsNullOrWhiteSpace(request.Action))
            return Result.Failure(
                new Error(DomainErrorCodes.Infrastructure.AuditWriteFailed, "Action is required.")
            );

        return await _writer.WriteAsync(request, cancellationToken);
    }
}
