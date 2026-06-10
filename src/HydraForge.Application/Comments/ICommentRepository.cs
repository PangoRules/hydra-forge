using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.Comments;

public interface ICommentRepository
{
    Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken ct = default);
    Task<IReadOnlyList<Comment>> ListByCardAsync(Guid cardId, CancellationToken ct = default);
    Task AddAsync(Comment comment, CancellationToken ct = default);
    Task UpdateAsync(Comment comment, CancellationToken ct = default);
}