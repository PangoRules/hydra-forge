using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Columns;

public class ColumnService(
    IColumnRepository columnRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher,
    IAuditLogWriter auditLogWriter
)
{
    private readonly IProjectBoardEventPublisher _publisher = publisher;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    public async Task<Result<ColumnDto>> CreateAsync(
        CreateColumnCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ColumnDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var existing = await columnRepo.GetByProjectIdAsync(cmd.ProjectId, ct);
        var maxPosition = existing.Count == 0 ? -1 : existing.Max(c => c.Position);

        var column = new Column
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
            Name = cmd.Name,
            Color = cmd.Color,
            WipLimit = cmd.WipLimit,
            Position = maxPosition + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await columnRepo.AddAsync(column, ct);
        await snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);
        await PublishAsync(cmd.ProjectId, BoardEntityType.Column, column.Id, BoardAction.Created, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Column",
                column.Id,
                "Created",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<ColumnDto>.Success(MapToDto(column));
    }

    public async Task<Result<ColumnDto>> GetByIdAsync(
        Guid projectId,
        Guid columnId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<ColumnDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var column = await columnRepo.GetByIdAsync(columnId, ct);
        if (column == null || column.ProjectId != projectId)
            return Result<ColumnDto>.Failure(
                new Error(DomainErrorCodes.Columns.NotFound, "Column not found.")
            );

        return Result<ColumnDto>.Success(MapToDto(column));
    }

    public async Task<Result<IReadOnlyList<ColumnDto>>> GetAllByProjectAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<ColumnDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var columns = await columnRepo.GetByProjectIdAsync(projectId, ct);
        return Result<IReadOnlyList<ColumnDto>>.Success([.. columns.Select(MapToDto)]);
    }

    public async Task<Result<ColumnDto>> UpdateAsync(
        UpdateColumnCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ColumnDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var column = await columnRepo.GetByIdAsync(cmd.ColumnId, ct);
        if (column == null || column.ProjectId != cmd.ProjectId)
            return Result<ColumnDto>.Failure(
                new Error(DomainErrorCodes.Columns.NotFound, "Column not found.")
            );

        column.UpdateDetails(cmd.Name, cmd.Color, cmd.WipLimit);

        await columnRepo.UpdateAsync(column, ct);
        await snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);
        await PublishAsync(cmd.ProjectId, BoardEntityType.Column, column.Id, BoardAction.Updated, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Column",
                column.Id,
                "Updated",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<ColumnDto>.Success(MapToDto(column));
    }

    public async Task<Result> DeleteAsync(DeleteColumnCommand cmd, CancellationToken ct = default)
    {
        var membership = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var column = await columnRepo.GetByIdAsync(cmd.ColumnId, ct);
        if (column == null || column.ProjectId != cmd.ProjectId)
            return Result.Failure(
                new Error(DomainErrorCodes.Columns.NotFound, "Column not found.")
            );

        var cardCount = await cardRepo.CountByColumnIdAsync(cmd.ColumnId, ct);
        if (cardCount > 0)
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Columns.DeleteNonEmpty,
                    "Cannot delete column with cards."
                )
            );

        await columnRepo.DeleteAsync(cmd.ColumnId, ct);
        await snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        var remaining = await columnRepo.GetByProjectIdAsync(cmd.ProjectId, ct);
        for (var i = 0; i < remaining.Count; i++)
        {
            remaining[i].AssignPosition(i);
            await columnRepo.UpdateAsync(remaining[i], ct);
        }

        await PublishAsync(cmd.ProjectId, BoardEntityType.Column, cmd.ColumnId, BoardAction.Deleted, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Column",
                cmd.ColumnId,
                "Deleted",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    public async Task<Result> ReorderAsync(
        ReorderColumnsCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var existing = await columnRepo.GetByProjectIdAsync(cmd.ProjectId, ct);

        if (cmd.ColumnIds.Count != existing.Count)
            return Result.Failure(
                new Error(DomainErrorCodes.Columns.InvalidPosition, "Invalid column positions.")
            );

        var existingIds = existing.Select(c => c.Id).ToHashSet();
        foreach (var id in cmd.ColumnIds)
        {
            if (!existingIds.Contains(id))
                return Result.Failure(
                    new Error(DomainErrorCodes.Columns.InvalidPosition, "Invalid column positions.")
                );
        }

        await columnRepo.ReorderAsync(cmd.ProjectId, cmd.ColumnIds, ct);
        await snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);
        await PublishAsync(cmd.ProjectId, BoardEntityType.Column, Guid.Empty, BoardAction.Moved, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Column",
                Guid.Empty,
                "Reordered",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    private static ColumnDto MapToDto(Column column) =>
        new(column.Id, column.Name, column.Position, column.WipLimit, column.Color);

    private async Task PublishAsync(Guid projectId, BoardEntityType entityType, Guid entityId, BoardAction action, CancellationToken ct)
    {
        var envelope = new ProjectBoardEventEnvelope(
            Guid.NewGuid(),
            projectId,
            entityType,
            entityId,
            action,
            1,
            DateTime.UtcNow,
            null!
        );
        await _publisher.PublishAsync(envelope, ct);
    }
}

