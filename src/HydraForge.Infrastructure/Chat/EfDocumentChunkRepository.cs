namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

public sealed class EfDocumentChunkRepository(HydraForgeDbContext context)
    : IDocumentChunkRepository
{
    public async Task AddAsync(DocumentChunk chunk, CancellationToken ct = default)
    {
        context.DocumentChunks.Add(chunk);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken ct = default
    )
    {
        context.DocumentChunks.AddRange(chunks);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        Guid userId,
        IReadOnlyList<Guid>? sessionDocumentIds,
        ReadOnlyMemory<float> queryEmbedding,
        int k,
        CancellationToken ct = default
    )
    {
        var vector = new Vector(queryEmbedding.Span.ToArray());

        if (sessionDocumentIds != null)
        {
            var docIdList = sessionDocumentIds.ToList();
            return await context
                .DocumentChunks.FromSqlInterpolated(
                    $"""
                    SELECT * FROM document_chunks
                    WHERE document_id = ANY({docIdList})
                      AND user_id = {userId}
                    ORDER BY embedding <=> {vector}
                    LIMIT {k}
                    """
                )
                .ToListAsync(ct);
        }

        return await context
            .DocumentChunks.FromSqlInterpolated(
                $"""
                SELECT dc.* FROM document_chunks dc
                JOIN documents d ON d.id = dc.document_id
                WHERE d.user_id = {userId}
                ORDER BY dc.embedding <=> {vector}
                LIMIT {k}
                """
            )
            .ToListAsync(ct);
    }
}
