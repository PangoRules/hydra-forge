using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public sealed class CardChatLinkService(
    ICardChatLinkRepository linkRepo,
    ICardRepository cardRepo,
    IUserRepository userRepo,
    IProjectMemberRepository memberRepo
) : ICardChatLinkService
{
    private readonly ICardChatLinkRepository _linkRepo = linkRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;

    public async Task<Result<IReadOnlyList<CardChatLinkDto>>> GetByCardAsync(
        Guid cardId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null)
            return Result<IReadOnlyList<CardChatLinkDto>>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (
            !await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                card.ProjectId,
                userId,
                ct
            )
        )
            return Result<IReadOnlyList<CardChatLinkDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Project member required.")
            );

        var links = await _linkRepo.GetByCardAsync(cardId, ct);

        var ownerIds = links.Select(l => l.OwnerId).Distinct().ToList();
        var owners = await _userRepo.FindByIdsAsync(ownerIds, ct);

        var summaries = links
            .Select(l => new CardChatLinkDto(
                l.Id,
                l.CardId,
                l.ChatSessionId,
                l.OwnerId,
                owners.TryGetValue(l.OwnerId, out var u) ? u.Username : "?",
                l.Summary.Length > 100 ? l.Summary[..100] : l.Summary,
                l.CreatedAt,
                l.ArchivedAt
            ))
            .ToList();

        return Result<IReadOnlyList<CardChatLinkDto>>.Success(summaries);
    }

    public async Task<Result<CardChatLinkDto>> ArchiveAsync(
        Guid linkId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var link = await _linkRepo.GetByIdAsync(linkId, ct);
        if (link == null)
            return Result<CardChatLinkDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Chat link not found.")
            );

        if (link.OwnerId != userId)
            return Result<CardChatLinkDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Only the owner can archive this chat link."
                )
            );

        await _linkRepo.ArchiveAsync(linkId, ct);

        var owners = await _userRepo.FindByIdsAsync([link.OwnerId], ct);
        var summary = new CardChatLinkDto(
            link.Id,
            link.CardId,
            link.ChatSessionId,
            link.OwnerId,
            owners.TryGetValue(link.OwnerId, out var u) ? u.Username : "?",
            link.Summary.Length > 100 ? link.Summary[..100] : link.Summary,
            link.CreatedAt,
            link.ArchivedAt
        );

        return Result<CardChatLinkDto>.Success(summary);
    }
}
