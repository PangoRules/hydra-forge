using HydraForge.Application.Audit;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Audit;

public class EfAuditLogReader(HydraForgeDbContext db) : IAuditLogReader
{
    public async Task<AuditLogQueryResult> QueryAsync(
        AuditLogQuery query,
        CancellationToken ct = default
    )
    {
        var q = db.AuditLogEntries.AsQueryable();

        if (query.ProjectId.HasValue)
            q = q.Where(e => e.ProjectId == query.ProjectId.Value);
        if (query.ActorId.HasValue)
            q = q.Where(e => e.ActorId == query.ActorId.Value);
        if (!string.IsNullOrWhiteSpace(query.EntityType))
            q = q.Where(e => e.EntityType == query.EntityType);
        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(e => e.Action == query.Action);
        if (query.From.HasValue)
            q = q.Where(e => e.Timestamp >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(e => e.Timestamp <= query.To.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q.OrderByDescending(e => e.Timestamp)
            .Skip(query.Skip)
            .Take(query.Take)
            .GroupJoin(
                db.Users,
                entry => entry.ActorId,
                user => user.Id,
                (entry, users) => new { entry, users }
            )
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, user) => new { x.entry, user })
            .GroupJoin(
                db.Projects,
                x => x.entry.ProjectId,
                project => (Guid?)project.Id,
                (x, projects) =>
                    new
                    {
                        x.entry,
                        x.user,
                        projects,
                    }
            )
            .SelectMany(
                x => x.projects.DefaultIfEmpty(),
                (x, project) =>
                    new AuditLogEntryDto(
                        x.entry.Id,
                        x.entry.ProjectId,
                        project == null ? null : project.Name,
                        x.entry.ActorId,
                        x.user == null ? "(deleted)" : x.user.Username,
                        x.entry.EntityType,
                        x.entry.EntityId,
                        x.entry.Action,
                        x.entry.OldValue,
                        x.entry.NewValue,
                        x.entry.Timestamp,
                        x.entry.Scope.ToString()
                    )
            )
            .ToListAsync(ct);

        return new AuditLogQueryResult(items, totalCount);
    }
}
