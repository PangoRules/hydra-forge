using HydraForge.Domain.Common;

namespace HydraForge.Application.Chat;

public interface ICardChatLinkService
{
    Task<Result<IReadOnlyList<CardChatLinkDto>>> GetByCardAsync(
        Guid cardId,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result<CardChatLinkDto>> ArchiveAsync(
        Guid linkId,
        Guid userId,
        CancellationToken ct = default
    );
}
