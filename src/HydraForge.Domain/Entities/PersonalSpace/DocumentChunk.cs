using Pgvector;

namespace HydraForge.Domain.Entities.PersonalSpace;

public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = new(new float[1536]);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
