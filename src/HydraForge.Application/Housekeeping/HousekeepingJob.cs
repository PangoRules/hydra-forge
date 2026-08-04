using HydraForge.Application.Attachments;
using HydraForge.Application.Audit;
using HydraForge.Application.Logging;
using HydraForge.Application.Settings;
using HydraForge.Domain.Constants;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HydraForge.Application.Housekeeping;

/// <summary>
/// Daily Hangfire recurring job — hard-deletes archived items, audit log entries,
/// usage records, and notifications past their configured retention window, then
/// best-effort cleans up the file blobs those rows referenced.
/// </summary>
public sealed class HousekeepingJob(
    ISettingsProvider settingsProvider,
    IHousekeepingRepository repository,
    IFileStore fileStore,
    IAuditLogWriter auditLogWriter,
    ILogger<HousekeepingJob> logger,
    IWarnLogger? warnLogger = null
)
{
    private readonly IWarnLogger _warnLogger = warnLogger ?? new NullWarnLogger();

    public async Task RunAsync(CancellationToken ct = default)
    {
        var settings = await settingsProvider.GetAsync(ct);
        var now = DateTime.UtcNow;
        var cutoffs = new HousekeepingCutoffs(
            now.AddDays(-settings.ArchivedItemRetentionDays),
            now.AddDays(-settings.AuditLogRetentionDays),
            now.AddDays(-settings.NotificationRetentionDays)
        );

        var result = await repository.RunAsync(cutoffs, ct);

        await Parallel.ForEachAsync(
            result.FilePathsToDelete,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (path, token) =>
            {
                try
                {
                    var deleteResult = await fileStore.DeleteAsync(path, token);
                    if (deleteResult.IsFailure)
                        _warnLogger.LogWarning(
                            $"Housekeeping: failed to delete file blob '{path}': {deleteResult.Error.Message}"
                        );
                }
                catch (Exception ex)
                {
                    _warnLogger.LogWarning(
                        $"Housekeeping: failed to delete file blob '{path}': {ex.Message}"
                    );
                }
            }
        );

        var auditResult = await auditLogWriter.WriteAsync(
            new AuditLogRequest(
                SystemActor.Id,
                AuditLogScope.System,
                "Housekeeping",
                Guid.NewGuid(),
                "Housekeeping run",
                null,
                null,
                AuditSnapshot.Serialize(result.Counts)
            ),
            ct
        );
        if (auditResult.IsFailure)
            _warnLogger.LogWarning(
                $"Housekeeping: failed to write summary audit entry: {auditResult.Error.Message}"
            );

        logger.LogInformation(
            "Housekeeping run complete: {Total} rows deleted, {FileCount} file blobs removed",
            result.Counts.Total,
            result.FilePathsToDelete.Count
        );
    }
}
