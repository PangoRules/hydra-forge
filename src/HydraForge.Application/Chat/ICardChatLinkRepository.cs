using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public interface ICardChatLinkRepository
{
    Task<CardChatLink?> GetByIdAsync(Guid linkId, CancellationToken ct = default);
    Task<IReadOnlyList<CardChatLink>> GetByCardAsync(Guid cardId, CancellationToken ct = default);
    Task AddAsync(CardChatLink link, CancellationToken ct = default);
    Task ArchiveAsync(Guid linkId, CancellationToken ct = default);
}
