namespace HydraForge.Application.Documents;

public sealed record SpecPlanHtmlBackfillResult(int SpecRowsConverted, int PlanRowsConverted);

/// <summary>
/// One-time backfill for Spec/Plan (and their version history) rows saved before
/// the server started normalizing Web UI HTML content to Markdown on write.
/// See <see cref="SpecPlanHtmlBackfillJob"/>.
/// </summary>
public interface ISpecPlanHtmlBackfillRepository
{
    Task<SpecPlanHtmlBackfillResult> RunAsync(CancellationToken ct = default);
}
