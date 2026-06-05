using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Projects;

public class EfProjectRepository(HydraForgeDbContext context) : IProjectRepository
{
    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        context.Projects.Add(project);
        await context.SaveChangesAsync(ct);
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Projects
            .Include(p => p.Columns)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Project>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var projectIds = await context.ProjectMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ProjectId)
            .ToListAsync(ct);

        return await context.Projects
            .Where(p => projectIds.Contains(p.Id))
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        context.Projects.Update(project);
        await context.SaveChangesAsync(ct);
    }
}

public class EfColumnRepository(HydraForgeDbContext context) : IColumnRepository
{
    public async Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        return await context.Columns
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default)
    {
        context.Columns.AddRange(columns);
        await context.SaveChangesAsync(ct);
    }
}

public class EfProjectMemberRepository(HydraForgeDbContext context) : IProjectMemberRepository
{
    public async Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
    {
        return await context.ProjectMembers
            .Include(m => m.User)
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        if (idList.Count == 0)
            return EmptyDictionary();

        var counts = await context.ProjectMembers
            .Where(m => idList.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => x.ProjectId, x => x.Count);
    }

    private static IReadOnlyDictionary<Guid, int> EmptyDictionary() => new Dictionary<Guid, int>();

    public async Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        context.ProjectMembers.Add(member);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        context.ProjectMembers.Update(member);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
    {
        var member = await context.ProjectMembers.FindAsync([id], ct);
        if (member != null)
        {
            context.ProjectMembers.Remove(member);
            await context.SaveChangesAsync(ct);
        }
    }
}

public class EfProjectContextSnapshotRepository(HydraForgeDbContext context) : IProjectContextSnapshotRepository
{
    public async Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        return await context.ProjectContextSnapshots
            .FirstOrDefaultAsync(s => s.ProjectId == projectId, ct);
    }

    public async Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        context.ProjectContextSnapshots.Add(snapshot);
        await context.SaveChangesAsync(ct);
    }
}