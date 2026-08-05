using HydraForge.Application.Attachments;
using HydraForge.Application.Audit;
using HydraForge.Application.Housekeeping;
using HydraForge.Application.Logging;
using HydraForge.Application.Settings;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HydraForge.Application.Tests.Housekeeping;

public class HousekeepingJobTests
{
    private static HousekeepingCounts EmptyCounts() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static (
        ISettingsProvider settingsProvider,
        IHousekeepingRepository repository,
        IFileStore fileStore,
        IAuditLogWriter auditLogWriter,
        IWarnLogger warnLogger,
        HousekeepingJob job
    ) CreateJob()
    {
        var settingsProvider = Substitute.For<ISettingsProvider>();
        settingsProvider
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(
                new SystemSettings
                {
                    ArchivedItemRetentionDays = 730,
                    AuditLogRetentionDays = 90,
                    NotificationRetentionDays = 30,
                }
            );

        var repository = Substitute.For<IHousekeepingRepository>();
        repository
            .RunAsync(Arg.Any<HousekeepingCutoffs>(), Arg.Any<CancellationToken>())
            .Returns(new HousekeepingRunResult(EmptyCounts(), []));

        var fileStore = Substitute.For<IFileStore>();
        var auditLogWriter = Substitute.For<IAuditLogWriter>();
        auditLogWriter
            .WriteAsync(Arg.Any<AuditLogRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var warnLogger = Substitute.For<IWarnLogger>();

        var job = new HousekeepingJob(
            settingsProvider,
            repository,
            fileStore,
            auditLogWriter,
            Substitute.For<ILogger<HousekeepingJob>>(),
            warnLogger
        );

        return (settingsProvider, repository, fileStore, auditLogWriter, warnLogger, job);
    }

    [Fact]
    public async Task RunAsync_ComputesCutoffsFromSettings()
    {
        var (_, repository, _, _, _, job) = CreateJob();
        var before = DateTime.UtcNow;

        await job.RunAsync();

        var after = DateTime.UtcNow;

        await repository
            .Received(1)
            .RunAsync(
                Arg.Is<HousekeepingCutoffs>(c =>
                    c.ArchivedItemCutoff >= before.AddDays(-730)
                    && c.ArchivedItemCutoff <= after.AddDays(-730)
                    && c.AuditLogCutoff >= before.AddDays(-90)
                    && c.AuditLogCutoff <= after.AddDays(-90)
                    && c.NotificationCutoff >= before.AddDays(-30)
                    && c.NotificationCutoff <= after.AddDays(-30)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunAsync_DeletesEachReturnedFilePath()
    {
        var (_, repository, fileStore, _, _, job) = CreateJob();
        repository
            .RunAsync(Arg.Any<HousekeepingCutoffs>(), Arg.Any<CancellationToken>())
            .Returns(new HousekeepingRunResult(EmptyCounts(), ["a/b/c", "d/e/f"]));
        fileStore
            .DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await job.RunAsync();

        await fileStore.Received(1).DeleteAsync("a/b/c", Arg.Any<CancellationToken>());
        await fileStore.Received(1).DeleteAsync("d/e/f", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_FileDeleteFailure_WarnsButDoesNotThrow()
    {
        var (_, repository, fileStore, _, warnLogger, job) = CreateJob();
        repository
            .RunAsync(Arg.Any<HousekeepingCutoffs>(), Arg.Any<CancellationToken>())
            .Returns(new HousekeepingRunResult(EmptyCounts(), ["broken/path"]));
        fileStore
            .DeleteAsync("broken/path", Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("FILE_NOT_FOUND", "missing")));

        await job.RunAsync();

        warnLogger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("broken/path")));
    }

    [Fact]
    public async Task RunAsync_FileDeleteThrows_WarnsButDoesNotThrow()
    {
        var (_, repository, fileStore, _, warnLogger, job) = CreateJob();
        repository
            .RunAsync(Arg.Any<HousekeepingCutoffs>(), Arg.Any<CancellationToken>())
            .Returns(new HousekeepingRunResult(EmptyCounts(), ["exploding/path"]));
        fileStore
            .DeleteAsync("exploding/path", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result>(new InvalidOperationException("boom")));

        await job.RunAsync();

        warnLogger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("exploding/path")));
    }

    [Fact]
    public async Task RunAsync_WritesSystemScopedAuditSummary()
    {
        var (_, _, _, auditLogWriter, _, job) = CreateJob();

        await job.RunAsync();

        await auditLogWriter
            .Received(1)
            .WriteAsync(
                Arg.Is<AuditLogRequest>(r =>
                    r.ActorId != Guid.Empty
                    && r.Scope == Domain.Enums.AuditLogScope.System
                    && r.EntityType == "Housekeeping"
                    && r.Action == "Housekeeping run"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunAsync_AuditWriteFailure_WarnsButDoesNotThrow()
    {
        var (_, _, _, auditLogWriter, warnLogger, job) = CreateJob();
        auditLogWriter
            .WriteAsync(Arg.Any<AuditLogRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("AUDIT_WRITE_FAILED", "db down")));

        await job.RunAsync();

        warnLogger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("audit")));
    }
}
