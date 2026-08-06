using HydraForge.Application.Documents;
using HydraForge.Application.Settings;
using HydraForge.Domain.Entities.PersonalSpace;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HydraForge.Application.Tests.Documents;

public class SpecPlanHtmlBackfillJobTests
{
    private static (
        ISpecPlanHtmlBackfillRepository repository,
        ISettingsRepository settingsRepository,
        SpecPlanHtmlBackfillJob job
    ) CreateJob(SystemSettings settings)
    {
        var repository = Substitute.For<ISpecPlanHtmlBackfillRepository>();
        repository
            .RunAsync(Arg.Any<CancellationToken>())
            .Returns(new SpecPlanHtmlBackfillResult(0, 0));

        var settingsRepository = Substitute.For<ISettingsRepository>();
        settingsRepository.GetSingletonAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var job = new SpecPlanHtmlBackfillJob(
            repository,
            settingsRepository,
            Substitute.For<ILogger<SpecPlanHtmlBackfillJob>>()
        );

        return (repository, settingsRepository, job);
    }

    [Fact]
    public async Task RunAsync_AlreadyCompleted_SkipsBackfillAndDoesNotRewriteSettings()
    {
        var settings = new SystemSettings { SpecPlanHtmlBackfillCompletedAt = DateTime.UtcNow };
        var (repository, settingsRepository, job) = CreateJob(settings);

        await job.RunAsync();

        await repository.DidNotReceive().RunAsync(Arg.Any<CancellationToken>());
        await settingsRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<SystemSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NotYetCompleted_RunsBackfillAndMarksCompleted()
    {
        var settings = new SystemSettings { SpecPlanHtmlBackfillCompletedAt = null };
        var (repository, settingsRepository, job) = CreateJob(settings);

        await job.RunAsync();

        await repository.Received(1).RunAsync(Arg.Any<CancellationToken>());
        Assert.NotNull(settings.SpecPlanHtmlBackfillCompletedAt);
        await settingsRepository
            .Received(1)
            .UpdateAsync(
                Arg.Is<SystemSettings>(s => s.SpecPlanHtmlBackfillCompletedAt != null),
                Arg.Any<CancellationToken>()
            );
    }
}
