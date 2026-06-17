using HydraForge.Application.Specs;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Specs;

public class EfSpecRepository(HydraForgeDbContext context) : ISpecRepository
{
    private readonly HydraForgeDbContext _context = context;

    public async Task<Spec?> GetByIdAsync(Guid specId, CancellationToken ct = default)
    {
        return await _context.Specs.FirstOrDefaultAsync(s => s.Id == specId, ct);
    }

    public async Task<IReadOnlyList<Spec>> ListByProjectAsync(Guid projectId, SpecListFilter filter, CancellationToken ct = default)
    {
        var query = _context.Specs.Where(s => s.ProjectId == projectId);
        return await query.OrderBy(s => s.CreatedAt).ToListAsync(ct);
    }

    public async Task<SpecVersion?> GetVersionAsync(Guid specId, int version, CancellationToken ct = default)
    {
        return await _context.SpecVersions
            .FirstOrDefaultAsync(v => v.SpecId == specId && v.Version == version, ct);
    }

    public async Task<IReadOnlyList<SpecVersion>> ListVersionsAsync(Guid specId, CancellationToken ct = default)
    {
        return await _context.SpecVersions
            .Where(v => v.SpecId == specId)
            .OrderBy(v => v.Version)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetLinkedCardIdsAsync(Guid projectId, CancellationToken ct = default)
    {
        var linkedCards = await _context.Cards
            .Where(c => c.SpecId != null && c.ProjectId == projectId)
            .ToListAsync(ct);
        return linkedCards.ToDictionary(c => c.SpecId!.Value, c => (Guid?)c.Id);
    }

    public async Task<Guid?> GetLinkedCardIdAsync(Guid specId, CancellationToken ct = default)
    {
        var card = await _context.Cards.FirstOrDefaultAsync(c => c.SpecId == specId, ct);
        return card?.Id;
    }

    public async Task AddAsync(Spec spec, CancellationToken ct = default)
    {
        _context.Specs.Add(spec);
    }

    public async Task AddVersionAsync(SpecVersion version, CancellationToken ct = default)
    {
        _context.SpecVersions.Add(version);
    }

    public async Task UpdateAsync(Spec spec, CancellationToken ct = default)
    {
        _context.Specs.Update(spec);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
