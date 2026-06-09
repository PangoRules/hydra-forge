using HydraForge.Application.Projects;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Projects;

public class EfCardRepository(HydraForgeDbContext context) : ICardRepository
{
    public async Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
    {
        return await context
            .Cards.CountAsync(c => c.ColumnId == columnId && c.ArchivedAt == null, ct);
    }
}