using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
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
        return await context
            .Projects.Include(p => p.Columns)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<ProjectListPage> ListByUserIdAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        MemberRole? role,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        var query =
            from p in context.Projects
            join m in context.ProjectMembers on p.Id equals m.ProjectId
            where m.UserId == userId
            select new { Project = p, MemberRole = m.Role };

        if (!includeArchived)
            query = query.Where(x => x.Project.ArchivedAt == null);

        if (role.HasValue)
            query = query.Where(x => x.MemberRole == role.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Project.Name, $"%{search}%")
                || (
                    x.Project.Description != null
                    && EF.Functions.ILike(x.Project.Description, $"%{search}%")
                )
            );
        }

        var totalCount = await query.CountAsync(ct);

        query = sortBy switch
        {
            ProjectSortField.Name => sortDescending
                ? query.OrderByDescending(x => x.Project.Name)
                : query.OrderBy(x => x.Project.Name),
            ProjectSortField.UpdatedAt => sortDescending
                ? query.OrderByDescending(x => x.Project.UpdatedAt)
                : query.OrderBy(x => x.Project.UpdatedAt),
            _ => sortDescending
                ? query.OrderByDescending(x => x.Project.CreatedAt)
                : query.OrderBy(x => x.Project.CreatedAt),
        };

        var items = await query.Skip(skip).Take(take).Select(x => x.Project).ToListAsync(ct);

        return new ProjectListPage(items, totalCount);
    }

    public async Task<ProjectListPage> ListAllAsync(
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        var query = context.Projects.AsQueryable();

        if (!includeArchived)
            query = query.Where(p => p.ArchivedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, $"%{search}%")
                || (p.Description != null && EF.Functions.ILike(p.Description, $"%{search}%"))
            );

        var totalCount = await query.CountAsync(ct);

        query = sortBy switch
        {
            ProjectSortField.Name => sortDescending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),
            ProjectSortField.UpdatedAt => sortDescending
                ? query.OrderByDescending(p => p.UpdatedAt)
                : query.OrderBy(p => p.UpdatedAt),
            _ => sortDescending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
        };

        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return new ProjectListPage(items, totalCount);
    }

    public async Task<ProjectListPage> ListNonMemberProjectsAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        // Left join: projects that have NO matching membership row for userId
        var query =
            from p in context.Projects
            join m in context.ProjectMembers
                on new { ProjectId = p.Id, UserId = userId } equals new { m.ProjectId, m.UserId }
                into gj
            from m in gj.DefaultIfEmpty()
            where m == null // null membership row = not a member
            select p;

        if (!includeArchived)
            query = query.Where(p => p.ArchivedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, $"%{search}%")
                || (p.Description != null && EF.Functions.ILike(p.Description, $"%{search}%"))
            );

        var totalCount = await query.CountAsync(ct);

        query = sortBy switch
        {
            ProjectSortField.Name => sortDescending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),
            ProjectSortField.UpdatedAt => sortDescending
                ? query.OrderByDescending(p => p.UpdatedAt)
                : query.OrderBy(p => p.UpdatedAt),
            _ => sortDescending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
        };

        var items = await query.Skip(skip).Take(take).ToListAsync(ct);
        return new ProjectListPage(items, totalCount);
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        context.Projects.Update(project);
        await context.SaveChangesAsync(ct);
    }
}

public class EfColumnRepository(HydraForgeDbContext context) : IColumnRepository
{
    public async Task<IReadOnlyList<Column>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken ct = default
    )
    {
        return await context
            .Columns.Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);
    }

    public async Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Columns.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task AddAsync(Column column, CancellationToken ct = default)
    {
        context.Columns.Add(column);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        context.Columns.Update(column);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var column = await context.Columns.FindAsync([id], ct);
        if (column != null)
        {
            context.Columns.Remove(column);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task ReorderAsync(
        Guid projectId,
        IReadOnlyList<Guid> orderedColumnIds,
        CancellationToken ct = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var columns = await context
                .Columns.Where(c => c.ProjectId == projectId)
                .ToListAsync(ct);

            var columnMap = columns.ToDictionary(c => c.Id);

            for (var i = 0; i < orderedColumnIds.Count; i++)
            {
                if (columnMap.TryGetValue(orderedColumnIds[i], out var col))
                    col.Position = i;
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            throw new InvalidOperationException("Failed to reorder columns.", ex);
        }
    }

    public async Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default)
    {
        context.Columns.AddRange(columns);
        await context.SaveChangesAsync(ct);
    }
}

public class EfProjectMemberRepository(HydraForgeDbContext context) : IProjectMemberRepository
{
    public async Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context
            .ProjectMembers.Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<ProjectMember?> GetByProjectAndUserAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await context.ProjectMembers.FirstOrDefaultAsync(
            m => m.ProjectId == projectId && m.UserId == userId,
            ct
        );
    }

    public async Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
        Guid projectId,
        CancellationToken ct = default
    )
    {
        return await context
            .ProjectMembers.Include(m => m.User)
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

        var counts = await context
            .ProjectMembers.Where(m => idList.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => x.ProjectId, x => x.Count);
    }

    public async Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
        IEnumerable<Guid> projectIds,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, MemberRole>();

        var roles = await context
            .ProjectMembers.Where(m => idList.Contains(m.ProjectId) && m.UserId == userId)
            .Select(m => new { m.ProjectId, m.Role })
            .ToListAsync(ct);

        return roles.ToDictionary(x => x.ProjectId, x => x.Role);
    }

    private static Dictionary<Guid, int> EmptyDictionary() => [];

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

public class EfProjectContextSnapshotRepository(HydraForgeDbContext context)
    : IProjectContextSnapshotRepository
{
    public async Task<ProjectContextSnapshot?> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken ct = default
    )
    {
        return await context.ProjectContextSnapshots.FirstOrDefaultAsync(
            s => s.ProjectId == projectId,
            ct
        );
    }

    public async Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        context.ProjectContextSnapshots.Add(snapshot);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default)
    {
        context.ProjectContextSnapshots.Update(snapshot);
        await context.SaveChangesAsync(ct);
    }
}
