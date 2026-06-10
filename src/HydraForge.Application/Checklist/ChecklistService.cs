using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Checklist;

public class ChecklistService(
    IChecklistItemRepository checklistRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter
)
{
    private readonly IChecklistItemRepository _checklistRepo = checklistRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;

    public async Task<Result<ChecklistItemDto>> CreateAsync(
        CreateChecklistItemCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (cmd.AssignedTo.HasValue)
        {
            var assigneeMember = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.AssignedTo.Value, ct);
            if (assigneeMember == null)
                return Result<ChecklistItemDto>.Failure(
                    new Error(DomainErrorCodes.Checklist.InvalidAssignee, "Assignee is not a project member.")
                );

            var assigneeUser = await _userRepo.FindByIdAsync(cmd.AssignedTo.Value, ct);
            if (assigneeUser == null || assigneeUser.IsDisabled)
                return Result<ChecklistItemDto>.Failure(
                    new Error(DomainErrorCodes.Checklist.InvalidAssignee, "Assignee user is disabled.")
                );
        }

        int position;
        if (cmd.Position.HasValue)
        {
            position = cmd.Position.Value;
            var existingItems = await _checklistRepo.ListByCardAsync(cmd.CardId, ct);
            if (position < 0 || position > existingItems.Count)
                return Result<ChecklistItemDto>.Failure(
                    new Error(DomainErrorCodes.Checklist.InvalidPosition, "Invalid position.")
                );

            if (position < existingItems.Count)
            {
                var toShift = existingItems.Where(i => i.Position >= position).ToList();
                foreach (var shiftingItem in toShift)
                    shiftingItem.Position += 1;
                await _checklistRepo.UpdatePositionsAsync(toShift, ct);
            }
        }
        else
        {
            position = await _checklistRepo.GetMaxPositionAsync(cmd.CardId, ct) + 1;
        }

        var item = new ChecklistItem
        {
            Id = Guid.NewGuid(),
            CardId = cmd.CardId,
            Text = cmd.Text,
            IsCompleted = false,
            Position = position,
            AssignedTo = cmd.AssignedTo,
            CreatedAt = DateTime.UtcNow,
        };

        await _checklistRepo.AddAsync(item, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "ChecklistItem",
                item.Id,
                "Created",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        string? assigneeUsername = null;
        if (cmd.AssignedTo.HasValue)
        {
            var user = await _userRepo.FindByIdAsync(cmd.AssignedTo.Value, ct);
            assigneeUsername = user?.Username;
        }

        return Result<ChecklistItemDto>.Success(
            new ChecklistItemDto(
                item.Id,
                item.CardId,
                item.Text,
                item.IsCompleted,
                item.Position,
                item.AssignedTo,
                assigneeUsername,
                item.CreatedAt
            )
        );
    }

    public async Task<Result<ChecklistItemDto>> UpdateAsync(
        UpdateChecklistItemCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var item = await _checklistRepo.GetByIdAsync(cmd.ItemId, ct);
        if (item == null || item.CardId != cmd.CardId)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Checklist.ItemNotFound, "Checklist item not found.")
            );

        if (cmd.AssignedTo.HasValue)
        {
            var assigneeMember = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.AssignedTo.Value, ct);
            if (assigneeMember == null)
                return Result<ChecklistItemDto>.Failure(
                    new Error(DomainErrorCodes.Checklist.InvalidAssignee, "Assignee is not a project member.")
                );

            var assigneeUser = await _userRepo.FindByIdAsync(cmd.AssignedTo.Value, ct);
            if (assigneeUser == null || assigneeUser.IsDisabled)
                return Result<ChecklistItemDto>.Failure(
                    new Error(DomainErrorCodes.Checklist.InvalidAssignee, "Assignee user is disabled.")
                );
        }

        item.Text = cmd.Text;
        item.AssignedTo = cmd.AssignedTo;
        await _checklistRepo.UpdateAsync(item, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "ChecklistItem",
                item.Id,
                "Updated",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        string? assigneeUsername = null;
        if (item.AssignedTo.HasValue)
        {
            var user = await _userRepo.FindByIdAsync(item.AssignedTo.Value, ct);
            assigneeUsername = user?.Username;
        }

        return Result<ChecklistItemDto>.Success(
            new ChecklistItemDto(
                item.Id,
                item.CardId,
                item.Text,
                item.IsCompleted,
                item.Position,
                item.AssignedTo,
                assigneeUsername,
                item.CreatedAt
            )
        );
    }

    public async Task<Result<ChecklistItemDto>> ToggleAsync(
        ToggleChecklistItemCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var item = await _checklistRepo.GetByIdAsync(cmd.ItemId, ct);
        if (item == null || item.CardId != cmd.CardId)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Checklist.ItemNotFound, "Checklist item not found.")
            );

        item.IsCompleted = !item.IsCompleted;
        await _checklistRepo.UpdateAsync(item, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "ChecklistItem",
                item.Id,
                item.IsCompleted ? "Completed" : "Uncompleted",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        string? assigneeUsername = null;
        if (item.AssignedTo.HasValue)
        {
            var user = await _userRepo.FindByIdAsync(item.AssignedTo.Value, ct);
            assigneeUsername = user?.Username;
        }

        return Result<ChecklistItemDto>.Success(
            new ChecklistItemDto(
                item.Id,
                item.CardId,
                item.Text,
                item.IsCompleted,
                item.Position,
                item.AssignedTo,
                assigneeUsername,
                item.CreatedAt
            )
        );
    }

    public async Task<Result<ChecklistItemDto>> ReorderAsync(
        ReorderChecklistItemCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var item = await _checklistRepo.GetByIdAsync(cmd.ItemId, ct);
        if (item == null || item.CardId != cmd.CardId)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Checklist.ItemNotFound, "Checklist item not found.")
            );

        var allItems = (await _checklistRepo.ListByCardAsync(cmd.CardId, ct))
            .OrderBy(i => i.Position)
            .ToList();

        if (cmd.NewPosition < 0 || cmd.NewPosition >= allItems.Count)
            return Result<ChecklistItemDto>.Failure(
                new Error(DomainErrorCodes.Checklist.InvalidPosition, "Invalid position.")
            );

        var oldPosition = item.Position;
        var newPosition = cmd.NewPosition;

        if (oldPosition == newPosition)
        {
            string? assigneeUsername2 = null;
            if (item.AssignedTo.HasValue)
            {
                var user = await _userRepo.FindByIdAsync(item.AssignedTo.Value, ct);
                assigneeUsername2 = user?.Username;
            }
            return Result<ChecklistItemDto>.Success(
                new ChecklistItemDto(
                    item.Id, item.CardId, item.Text, item.IsCompleted, item.Position,
                    item.AssignedTo, assigneeUsername2, item.CreatedAt
                )
            );
        }

        var toUpdate = new List<ChecklistItem>();
        foreach (var i in allItems)
        {
            if (i.Id == item.Id) continue;
            if (oldPosition < newPosition)
            {
                if (i.Position > oldPosition && i.Position <= newPosition)
                    i.Position -= 1;
            }
            else
            {
                if (i.Position >= newPosition && i.Position < oldPosition)
                    i.Position += 1;
            }
            toUpdate.Add(i);
        }
        item.Position = newPosition;
        toUpdate.Add(item);

        await _checklistRepo.UpdatePositionsAsync(toUpdate, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "ChecklistItem",
                item.Id,
                "Reordered",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        string? assigneeUsername = null;
        if (item.AssignedTo.HasValue)
        {
            var user = await _userRepo.FindByIdAsync(item.AssignedTo.Value, ct);
            assigneeUsername = user?.Username;
        }

        return Result<ChecklistItemDto>.Success(
            new ChecklistItemDto(
                item.Id, item.CardId, item.Text, item.IsCompleted, item.Position,
                item.AssignedTo, assigneeUsername, item.CreatedAt
            )
        );
    }

    public async Task<Result> DeleteAsync(
        DeleteChecklistItemCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result.Failure(new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));

        var item = await _checklistRepo.GetByIdAsync(cmd.ItemId, ct);
        if (item == null || item.CardId != cmd.CardId)
            return Result.Failure(
                new Error(DomainErrorCodes.Checklist.ItemNotFound, "Checklist item not found.")
            );

        var deletedPosition = item.Position;
        await _checklistRepo.DeleteAsync(cmd.ItemId, ct);
        await _checklistRepo.CompactPositionsAsync(cmd.CardId, deletedPosition, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "ChecklistItem",
                item.Id,
                "Deleted",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ChecklistItemDto>>> ListAsync(
        Guid projectId,
        Guid cardId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<ChecklistItemDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<IReadOnlyList<ChecklistItemDto>>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var items = await _checklistRepo.ListByCardAsync(cardId, ct);
        var dtos = new List<ChecklistItemDto>();

        foreach (var item in items.OrderBy(i => i.Position))
        {
            string? assigneeUsername = null;
            if (item.AssignedTo.HasValue)
            {
                var user = await _userRepo.FindByIdAsync(item.AssignedTo.Value, ct);
                assigneeUsername = user?.Username;
            }
            dtos.Add(new ChecklistItemDto(
                item.Id,
                item.CardId,
                item.Text,
                item.IsCompleted,
                item.Position,
                item.AssignedTo,
                assigneeUsername,
                item.CreatedAt
            ));
        }

        return Result<IReadOnlyList<ChecklistItemDto>>.Success(dtos);
    }
}