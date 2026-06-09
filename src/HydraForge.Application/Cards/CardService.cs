using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Cards;

public class CardService(
    ICardRepository cardRepo,
    ICardAssigneeRepository assigneeRepo,
    ICardWatcherRepository watcherRepo,
    ICardRelationshipRepository relationshipRepo,
    IColumnRepository columnRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter
)
{
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly ICardAssigneeRepository _assigneeRepo = assigneeRepo;
    private readonly ICardWatcherRepository _watcherRepo = watcherRepo;
    private readonly ICardRelationshipRepository _relationshipRepo = relationshipRepo;
    private readonly IColumnRepository _columnRepo = columnRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;

    public async Task<Result<CardDto>> CreateAsync(CreateCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var column = await _columnRepo.GetByIdAsync(cmd.ColumnId, ct);
        if (column == null || column.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Columns.NotFound, "Column not found.")
            );

        var maxNumber = await _cardRepo.GetMaxCardNumberAsync(cmd.ProjectId, ct);
        var cardCount = await _cardRepo.CountByColumnIdAsync(cmd.ColumnId, ct);

        Card? parentCard = null;
        if (cmd.ParentCardId.HasValue)
        {
            parentCard = await _cardRepo.GetByIdAsync(cmd.ParentCardId.Value, ct);
            if (parentCard == null)
                return Result<CardDto>.Failure(
                    new Error(DomainErrorCodes.Cards.NotFound, "Parent card not found.")
                );

            var parentError = Card.ValidateParentEpic(new Card { Id = Guid.Empty, ProjectId = cmd.ProjectId, Type = cmd.Type }, parentCard);
            if (parentError != null)
                return Result<CardDto>.Failure(parentError);
        }

        var card = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
            ColumnId = cmd.ColumnId,
            ParentCardId = cmd.ParentCardId,
            CardNumber = maxNumber + 1,
            Title = cmd.Title,
            Description = cmd.Description,
            Type = cmd.Type,
            Position = cardCount,
            DueAt = cmd.DueAt,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            MovedAt = DateTime.UtcNow,
        };

        await _cardRepo.AddAsync(card, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId,
            AuditLogScope.Project,
            "Card",
            card.Id,
            "Created",
            cmd.ProjectId,
            null,
            null
        ), ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> GetByIdAsync(Guid projectId, Guid cardId, Guid actorId, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> GetByNumberAsync(Guid projectId, int cardNumber, Guid actorId, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByProjectAndNumberAsync(projectId, cardNumber, ct);
        if (card == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<IReadOnlyList<CardDto>>> ListAsync(Guid projectId, CardListFilter filter, Guid actorId, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<CardDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var cards = await _cardRepo.ListByProjectAsync(projectId, filter, ct);
        var dtos = new List<CardDto>();
        foreach (var card in cards)
        {
            dtos.Add(await MapToDtoAsync(card, ct));
        }

        return Result<IReadOnlyList<CardDto>>.Success(dtos);
    }

    public async Task<Result<CardDto>> UpdateAsync(UpdateCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (card.Version != cmd.Version)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.ConcurrencyMismatch, "Card has been modified.")
            );

        if (card.ArchivedAt != null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.Archived, "Card is archived.")
            );

        Card? parentCard = null;
        if (cmd.ParentCardId.HasValue)
        {
            parentCard = await _cardRepo.GetByIdAsync(cmd.ParentCardId.Value, ct);
            if (parentCard == null)
                return Result<CardDto>.Failure(
                    new Error(DomainErrorCodes.Cards.NotFound, "Parent card not found.")
                );

            var parentError = Card.ValidateParentEpic(card, parentCard);
            if (parentError != null)
                return Result<CardDto>.Failure(parentError);
        }

        card.Title = cmd.Title;
        card.Description = cmd.Description;
        card.Type = cmd.Type;
        card.ParentCardId = cmd.ParentCardId;
        card.DueAt = cmd.DueAt;
        card.UpdatedAt = DateTime.UtcNow;
        card.Version += 1;

        await _cardRepo.UpdateAsync(card, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId,
            AuditLogScope.Project,
            "Card",
            card.Id,
            "Updated",
            cmd.ProjectId,
            null,
            null
        ), ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<BlockedMoveWarningDto>> GetBlockedMoveWarningAsync(Guid projectId, Guid cardId, Guid actorId, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<BlockedMoveWarningDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<BlockedMoveWarningDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var blockers = await _relationshipRepo.ListBlockersForCardAsync(cardId, ct);
        var blockerDtos = new List<BlockerDto>();

        foreach (var blocker in blockers)
        {
            var blockerCard = await _cardRepo.GetByIdAsync(blocker.SourceCardId, ct);
            if (blockerCard != null && blockerCard.ArchivedAt == null)
            {
                blockerDtos.Add(new BlockerDto(
                    blockerCard.Id,
                    blockerCard.CardNumber,
                    blockerCard.Title,
                    RelationshipBlockerType.BlockedBy
                ));
            }
        }

        var predecessors = await _relationshipRepo.ListPredecessorsAsync(cardId, ct);
        foreach (var pred in predecessors)
        {
            var predCard = await _cardRepo.GetByIdAsync(pred.TargetCardId, ct);
            if (predCard != null && predCard.ArchivedAt == null)
            {
                blockerDtos.Add(new BlockerDto(
                    predCard.Id,
                    predCard.CardNumber,
                    predCard.Title,
                    RelationshipBlockerType.Precedes
                ));
            }
        }

        return Result<BlockedMoveWarningDto>.Success(new BlockedMoveWarningDto(cardId, blockerDtos));
    }

    public async Task<Result<CardDto>> MoveAsync(MoveCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (card.Version != cmd.Version)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.ConcurrencyMismatch, "Card has been modified.")
            );

        var targetColumn = await _columnRepo.GetByIdAsync(cmd.TargetColumnId, ct);
        if (targetColumn == null || targetColumn.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Columns.NotFound, "Target column not found.")
            );

        var blockers = await _relationshipRepo.ListBlockersForCardAsync(cmd.CardId, ct);
        var hasBlockers = blockers.Any(b =>
        {
            var blockerCard = _cardRepo.GetByIdAsync(b.SourceCardId, ct).Result;
            return blockerCard != null && blockerCard.ArchivedAt == null;
        });

        if (hasBlockers && !cmd.ConfirmBlockedMove)
        {
            var warning = await GetBlockedMoveWarningAsync(cmd.ProjectId, cmd.CardId, cmd.ActorId, ct);
            if (warning.IsFailure)
                return Result<CardDto>.Failure(warning.Error);

            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.BlockedMoveWarning, "Card has blockers.")
            );
        }

        var oldColumnId = card.ColumnId;
        var oldPosition = card.Position;

if (oldColumnId == cmd.TargetColumnId)
        {
            if (oldPosition > cmd.TargetPosition)
            {
                var allCards = await _cardRepo.ListByProjectAsync(cmd.ProjectId, new CardListFilter(cmd.TargetColumnId, true), ct);
                var cardsToShift = allCards.Where(c => c.Position >= cmd.TargetPosition && c.Position < oldPosition && c.Id != card.Id).ToList();
                foreach (var c in cardsToShift)
                {
                    c.Position += 1;
                    await _cardRepo.UpdateAsync(c, ct);
                }
            }
            else
            {
                await _cardRepo.CompactColumnPositionsAsync(oldColumnId, oldPosition, ct);
                var allCards = await _cardRepo.ListByProjectAsync(cmd.ProjectId, new CardListFilter(cmd.TargetColumnId, true), ct);
                var cardsToShift = allCards.Where(c => c.Position >= cmd.TargetPosition && c.Id != card.Id).ToList();
                foreach (var c in cardsToShift)
                {
                    c.Position += 1;
                    await _cardRepo.UpdateAsync(c, ct);
                }
            }
        }
        else
        {
            await _cardRepo.CompactColumnPositionsAsync(oldColumnId, oldPosition, ct);
            var cardsInTarget = await _cardRepo.ListByProjectAsync(cmd.ProjectId, new CardListFilter(cmd.TargetColumnId, true), ct);
            foreach (var c in cardsInTarget.Where(c => c.Position >= cmd.TargetPosition))
            {
                c.Position += 1;
                await _cardRepo.UpdateAsync(c, ct);
            }
        }

        card.ColumnId = cmd.TargetColumnId;
        card.Position = cmd.TargetPosition;
        card.MovedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;
        card.Version += 1;

        await _cardRepo.UpdateAsync(card, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId,
            AuditLogScope.Project,
            "Card",
            card.Id,
            "Moved",
            cmd.ProjectId,
            null,
            null
        ), ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> AssignAsync(AssignCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var assigneeUser = await _userRepo.GetByIdAsync(cmd.AssigneeUserId, ct);
        if (assigneeUser == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.InvalidAssignee, "Assignee user not found.")
            );

        var existing = await _assigneeRepo.GetByCardAndUserAsync(cmd.CardId, cmd.AssigneeUserId, ct);
        if (existing != null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.DuplicateAssignee, "User is already assigned.")
            );

        var assignee = new CardAssignee
        {
            Id = Guid.NewGuid(),
            CardId = cmd.CardId,
            UserId = cmd.AssigneeUserId,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = cmd.ActorId,
        };
        await _assigneeRepo.AddAsync(assignee, ct);

        var watcher = await _watcherRepo.GetByCardAndUserAsync(cmd.CardId, cmd.AssigneeUserId, ct);
        if (watcher == null)
        {
            var newWatcher = new CardWatcher
            {
                CardId = cmd.CardId,
                UserId = cmd.AssigneeUserId,
                AddedAt = DateTime.UtcNow,
            };
            await _watcherRepo.AddAsync(newWatcher, ct);
        }

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId,
            AuditLogScope.Project,
            "Card",
            card.Id,
            "Assigned",
            cmd.ProjectId,
            null,
            null
        ), ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> UnassignAsync(UnassignCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var existing = await _assigneeRepo.GetByCardAndUserAsync(cmd.CardId, cmd.AssigneeUserId, ct);
        if (existing == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.InvalidAssignee, "Assignee not found.")
            );

        await _assigneeRepo.RemoveAsync(cmd.CardId, cmd.AssigneeUserId, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId,
            AuditLogScope.Project,
            "Card",
            card.Id,
            "Unassigned",
            cmd.ProjectId,
            null,
            null
        ), ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> ArchiveAsync(ArchiveCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (card.Version != cmd.Version)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.ConcurrencyMismatch, "Card has been modified.")
            );

        var oldPosition = card.Position;
        var oldColumnId = card.ColumnId;

        card.ArchivedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;
        card.Version += 1;

        await _cardRepo.UpdateAsync(card, ct);
        await _cardRepo.CompactColumnPositionsAsync(oldColumnId, oldPosition, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId,
            AuditLogScope.Project,
            "Card",
            card.Id,
            "Archived",
            cmd.ProjectId,
            null,
            null
        ), ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result> DeleteAsync(DeleteCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var oldPosition = card.Position;
        var oldColumnId = card.ColumnId;

        await _cardRepo.DeleteAsync(cmd.CardId, ct);
        await _cardRepo.CompactColumnPositionsAsync(oldColumnId, oldPosition, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId,
            AuditLogScope.Project,
            "Card",
            card.Id,
            "Deleted",
            cmd.ProjectId,
            null,
            null
        ), ct);

        return Result.Success();
    }

    private async Task<CardDto> MapToDtoAsync(Card card, CancellationToken ct)
    {
        var assignees = await _assigneeRepo.ListByCardAsync(card.Id, ct);
        var assigneeDtos = new List<CardAssigneeDto>();
        foreach (var a in assignees)
        {
            var user = await _userRepo.GetByIdAsync(a.UserId, ct);
            assigneeDtos.Add(new CardAssigneeDto(a.Id, a.UserId, user?.Username ?? string.Empty, a.AssignedAt));
        }

        var watchers = await _watcherRepo.ListByCardAsync(card.Id, ct);
        var watcherDtos = new List<CardWatcherDto>();
        foreach (var w in watchers)
        {
            var user = await _userRepo.GetByIdAsync(w.UserId, ct);
            watcherDtos.Add(new CardWatcherDto(w.UserId, user?.Username ?? string.Empty, w.AddedAt));
        }

        return new CardDto(
            card.Id,
            card.ProjectId,
            card.ColumnId,
            card.CardNumber,
            card.Title,
            card.Description,
            card.Type,
            card.Position,
            card.DueAt,
            card.Version,
            card.CreatedAt,
            card.UpdatedAt,
            card.MovedAt,
            card.ArchivedAt,
            card.ParentCardId,
            assigneeDtos,
            watcherDtos
        );
    }
}
