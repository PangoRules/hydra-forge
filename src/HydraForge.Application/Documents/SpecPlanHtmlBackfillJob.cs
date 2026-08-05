using HydraForge.Application.Settings;
using Microsoft.Extensions.Logging;

namespace HydraForge.Application.Documents;

/// <summary>
/// One-time Hangfire job (enqueued on every startup, no-ops after the first
/// successful run — see <see cref="SystemSettings.SpecPlanHtmlBackfillCompletedAt"/>)
/// converting Spec/Plan Content rows saved as raw TipTap HTML, before the server
/// started normalizing to Markdown on write, into Markdown. Existing docs render
/// correctly in the TUI (and any other Markdown-expecting client) without needing
/// a re-save from the Web UI.
/// </summary>
public sealed class SpecPlanHtmlBackfillJob(
    ISpecPlanHtmlBackfillRepository repository,
    ISettingsRepository settingsRepository,
    ILogger<SpecPlanHtmlBackfillJob> logger
)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var settings = await settingsRepository.GetSingletonAsync(ct);
        if (settings.SpecPlanHtmlBackfillCompletedAt != null)
            return;

        var result = await repository.RunAsync(ct);
        logger.LogInformation(
            "Spec/Plan HTML backfill converted {SpecRows} spec rows and {PlanRows} plan rows to Markdown",
            result.SpecRowsConverted,
            result.PlanRowsConverted
        );

        settings.MarkSpecPlanHtmlBackfillCompleted();
        await settingsRepository.UpdateAsync(settings, ct);
    }
}
