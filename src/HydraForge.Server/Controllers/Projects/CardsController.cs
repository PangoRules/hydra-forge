using HydraForge.Application.Cards;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using HydraForge.Application.Auth;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppCards = HydraForge.Application.Cards;

namespace HydraForge.Server.Controllers.Projects;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/projects/{projectId:guid}/[controller]")]
public class CardsController(CardService cardService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AppCards.CardListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid projectId,
        [FromQuery] Guid? columnId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] Guid? assigneeUserId = null,
        [FromQuery] CardType? type = null
    )
    {
        var userId = User.GetRequiredUserId();

        var filter = new AppCards.CardListFilter(columnId, includeArchived, assigneeUserId, type);
        var result = await cardService.ListAsync(projectId, filter, userId);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new AppCards.CardListResponse([.. result.Value.Select(MapToResponse)]);
        return Ok(response);
    }

    [HttpGet("{cardIdOrNumber}")]
    [ProducesResponseType(typeof(AppCards.CardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdOrNumber(Guid projectId, string cardIdOrNumber)
    {
        var userId = User.GetRequiredUserId();

        if (Guid.TryParse(cardIdOrNumber, out var cardId))
        {
            var result = await cardService.GetByIdAsync(projectId, cardId, userId);
            if (result.IsFailure)
            {
                return this.ToProblemResult(result.Error);
            }
            return Ok(MapToResponse(result.Value));
        }

        if (int.TryParse(cardIdOrNumber, out var cardNumber))
        {
            var result = await cardService.GetByNumberAsync(projectId, cardNumber, userId);
            if (result.IsFailure)
            {
                return this.ToProblemResult(result.Error);
            }
            return Ok(MapToResponse(result.Value));
        }

        return this.ToProblemResult(new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AppCards.CardResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] AppCards.CreateCardRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppCards.CreateCardCommand(
            projectId,
            request.ColumnId,
            userId,
            request.Title,
            request.Description,
            request.Type,
            request.ParentCardId,
            request.DueAt
        );
        var result = await cardService.CreateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = MapToResponse(result.Value);
        return CreatedAtAction(
            nameof(GetByIdOrNumber),
            new { projectId, cardIdOrNumber = result.Value.Id },
            response
        );
    }

    [HttpPut("{cardId:guid}")]
    [ProducesResponseType(typeof(AppCards.CardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid cardId,
        [FromBody] AppCards.UpdateCardRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppCards.UpdateCardCommand(
            projectId,
            cardId,
            userId,
            request.Title,
            request.Description,
            request.Type,
            request.ParentCardId,
            request.DueAt,
            request.Version
        );
        var result = await cardService.UpdateAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return Ok(MapToResponse(result.Value));
    }

    [HttpPost("{cardId:guid}/move")]
    [ProducesResponseType(typeof(AppCards.CardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppCards.BlockedMoveWarningResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Move(
        Guid projectId,
        Guid cardId,
        [FromBody] AppCards.MoveCardRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppCards.MoveCardCommand(
            projectId,
            cardId,
            request.TargetColumnId,
            request.TargetPosition,
            userId,
            request.ConfirmBlockedMove,
            request.Version
        );
        var result = await cardService.MoveAsync(cmd);

        if (result.IsFailure)
        {
            if (result.Error.Code == DomainErrorCodes.Cards.BlockedMoveWarning)
            {
                var warningResult = await cardService.GetBlockedMoveWarningAsync(
                    projectId,
                    cardId,
                    userId
                );
                if (warningResult.IsFailure)
                {
                    return this.ToProblemResult(warningResult.Error);
                }

                var warningResponse = new AppCards.BlockedMoveWarningResponse(
                    warningResult.Value.CardId,
                    warningResult
                        .Value.Blockers.Select(b => new AppCards.BlockerResponse(
                            b.CardId,
                            b.CardNumber,
                            b.Title,
                            b.BlockerType.ToString()
                        ))
                        .ToList()
                );
                return StatusCode(409, warningResponse);
            }
            return this.ToProblemResult(result.Error);
        }

        return Ok(MapToResponse(result.Value));
    }

    [HttpPost("{cardId:guid}/assignees")]
    [ProducesResponseType(typeof(AppCards.CardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(
        Guid projectId,
        Guid cardId,
        [FromBody] AppCards.AssignCardRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppCards.AssignCardCommand(projectId, cardId, request.AssigneeUserId, userId);
        var result = await cardService.AssignAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return Ok(MapToResponse(result.Value));
    }

    [HttpDelete("{cardId:guid}/assignees/{assigneeUserId:guid}")]
    [ProducesResponseType(typeof(AppCards.CardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unassign(Guid projectId, Guid cardId, Guid assigneeUserId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppCards.UnassignCardCommand(projectId, cardId, assigneeUserId, userId);
        var result = await cardService.UnassignAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return Ok(MapToResponse(result.Value));
    }

    [HttpPost("{cardId:guid}/archive")]
    [ProducesResponseType(typeof(AppCards.CardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(
        Guid projectId,
        Guid cardId,
        [FromBody] AppCards.ArchiveCardRequest request
    )
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppCards.ArchiveCardCommand(projectId, cardId, userId, request.Version);
        var result = await cardService.ArchiveAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return Ok(MapToResponse(result.Value));
    }

    [HttpDelete("{cardId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();

        var cmd = new AppCards.DeleteCardCommand(projectId, cardId, userId);
        var result = await cardService.DeleteAsync(cmd);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }

    private static AppCards.CardResponse MapToResponse(AppCards.CardDto dto) =>
        new(
            dto.Id,
            dto.ProjectId,
            dto.ColumnId,
            dto.CardNumber,
            dto.Title,
            dto.Description,
            dto.Type,
            dto.Position,
            dto.DueAt,
            dto.Version,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.MovedAt,
            dto.ArchivedAt,
            dto.ParentCardId,
            [
                .. dto.Assignees.Select(a => new AppCards.CardAssigneeResponse(
                    a.Id,
                    a.UserId,
                    a.Username,
                    a.AssignedAt
                )),
            ],
            [
                .. dto.Watchers.Select(w => new AppCards.CardWatcherResponse(
                    w.UserId,
                    w.Username,
                    AddedAt: w.AddedAt
                )),
            ]
        );
}
