namespace HydraForge.Application.Housekeeping;

/// <summary>
/// Hard-deletes archived/aged rows past their configured retention cutoff.
/// See docs/archive/specs/2026-06-03-archive-and-housekeeping-design.md §3 for the
/// per-entity classification this implements.
/// </summary>
public interface IHousekeepingRepository
{
    Task<HousekeepingRunResult> RunAsync(
        HousekeepingCutoffs cutoffs,
        CancellationToken ct = default
    );
}
