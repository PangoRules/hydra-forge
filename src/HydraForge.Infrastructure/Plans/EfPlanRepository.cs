using HydraForge.Application.Plans;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Plans;

public class EfPlanRepository(HydraForgeDbContext context) : IPlanRepository
{
    private readonly HydraForgeDbContext _context = context;

    public async Task<Plan?> GetByIdAsync(Guid planId, CancellationToken ct = default)
    {
        return await _context.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct);
    }

    public async Task<IReadOnlyList<Plan>> ListByProjectAsync(Guid projectId, PlanListFilter filter, CancellationToken ct = default)
    {
        var query = _context.Plans.Where(p => p.ProjectId == projectId);
        return await query.OrderBy(p => p.CreatedAt).ToListAsync(ct);
    }

    public async Task<PlanVersion?> GetVersionAsync(Guid planId, int version, CancellationToken ct = default)
    {
        return await _context.PlanVersions
            .FirstOrDefaultAsync(v => v.PlanId == planId && v.Version == version, ct);
    }

    public async Task<IReadOnlyList<PlanVersion>> ListVersionsAsync(Guid planId, CancellationToken ct = default)
    {
        return await _context.PlanVersions
            .Where(v => v.PlanId == planId)
            .OrderBy(v => v.Version)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetLinkedCardIdsAsync(Guid projectId, CancellationToken ct = default)
    {
        var linkedCards = await _context.Cards
            .Where(c => c.PlanId != null && c.ProjectId == projectId)
            .ToListAsync(ct);
        return linkedCards.ToDictionary(c => c.PlanId!.Value, c => (Guid?)c.Id);
    }

    public async Task<Guid?> GetLinkedCardIdAsync(Guid planId, CancellationToken ct = default)
    {
        var card = await _context.Cards.FirstOrDefaultAsync(c => c.PlanId == planId, ct);
        return card?.Id;
    }

    public async Task AddAsync(Plan plan, CancellationToken ct = default)
    {
        _context.Plans.Add(plan);
    }

    public async Task AddVersionAsync(PlanVersion version, CancellationToken ct = default)
    {
        _context.PlanVersions.Add(version);
    }

    public async Task UpdateAsync(Plan plan, CancellationToken ct = default)
    {
        _context.Plans.Update(plan);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
