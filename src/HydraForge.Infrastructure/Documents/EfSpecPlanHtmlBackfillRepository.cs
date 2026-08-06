using HydraForge.Application.Documents;
using HydraForge.Application.Shared;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Documents;

public class EfSpecPlanHtmlBackfillRepository(
    HydraForgeDbContext db,
    IHtmlToMarkdownConverter converter
) : ISpecPlanHtmlBackfillRepository
{
    // Every doc saved via the Web UI's TipTap editor is at minimum wrapped in a
    // block tag (<p>, <h1>, ...) — genuine Markdown authored via the TUI's $EDITOR
    // has no reason to open with a literal '<'. Good enough for a one-time,
    // best-effort backfill; the live write path (SpecsController/PlansController)
    // uses the explicit X-Content-Format header instead of sniffing.
    private static bool LooksLikeHtml(string content) => content.TrimStart().StartsWith('<');

    public async Task<SpecPlanHtmlBackfillResult> RunAsync(CancellationToken ct = default)
    {
        var specRowsConverted = 0;
        var planRowsConverted = 0;

        foreach (var spec in await db.Specs.ToListAsync(ct))
        {
            if (!LooksLikeHtml(spec.Content))
                continue;
            spec.Content = converter.Convert(spec.Content);
            specRowsConverted++;
        }

        foreach (var specVersion in await db.SpecVersions.ToListAsync(ct))
        {
            if (!LooksLikeHtml(specVersion.Content))
                continue;
            specVersion.Content = converter.Convert(specVersion.Content);
            specRowsConverted++;
        }

        foreach (var plan in await db.Plans.ToListAsync(ct))
        {
            if (!LooksLikeHtml(plan.Content))
                continue;
            plan.Content = converter.Convert(plan.Content);
            planRowsConverted++;
        }

        foreach (var planVersion in await db.PlanVersions.ToListAsync(ct))
        {
            if (!LooksLikeHtml(planVersion.Content))
                continue;
            planVersion.Content = converter.Convert(planVersion.Content);
            planRowsConverted++;
        }

        if (specRowsConverted > 0 || planRowsConverted > 0)
            await db.SaveChangesAsync(ct);

        return new SpecPlanHtmlBackfillResult(specRowsConverted, planRowsConverted);
    }
}
