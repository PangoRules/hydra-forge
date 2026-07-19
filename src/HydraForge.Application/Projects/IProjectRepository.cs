using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Projects;

public enum ProjectSortField
{
    Name,
    CreatedAt,
    UpdatedAt,
}

public record ProjectListPage(IReadOnlyList<Project> Items, int TotalCount);

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken ct = default);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectListPage> ListByUserIdAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        MemberRole? role,
        int skip,
        int take,
        CancellationToken ct = default
    );
    Task UpdateAsync(Project project, CancellationToken ct = default);
}

public interface IColumnRepository
{
    Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Column column, CancellationToken ct = default);
    Task UpdateAsync(Column column, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default);
}

public interface IProjectMemberRepository
{
    Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default);
    Task AddMemberAsync(ProjectMember member, CancellationToken ct = default);
    Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid id, CancellationToken ct = default);
}

public interface IProjectContextSnapshotRepository
{
    Task<ProjectContextSnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default);
    Task UpdateAsync(ProjectContextSnapshot snapshot, CancellationToken ct = default);
}