namespace HydraForge.Infrastructure.Audit;

using HydraForge.Application.Audit;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class EfAuditLogWriter(HydraForgeDbContext dbContext, ILogger<EfAuditLogWriter> logger)
    : IAuditLogWriter
{
    private readonly HydraForgeDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly ILogger<EfAuditLogWriter> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result> WriteAsync(
        AuditLogRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var entry = new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId ?? Guid.Empty,
                ActorId = request.ActorId,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Action = request.Action,
                OldValue = request.OldValueJson,
                NewValue = request.NewValueJson,
                Timestamp = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };

            _dbContext.AuditLogEntries.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Audit log entry written: {EntityType}/{EntityId} {Action} by {ActorId}",
                entry.EntityType,
                entry.EntityId,
                entry.Action,
                entry.ActorId
            );

            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Failed to write audit log entry for {EntityType}/{EntityId}",
                request.EntityType,
                request.EntityId
            );

            return Result.Failure(
                new Error(
                    DomainErrorCodes.Infrastructure.AuditWriteFailed,
                    "Audit log write failed."
                )
            );
        }
    }
}

