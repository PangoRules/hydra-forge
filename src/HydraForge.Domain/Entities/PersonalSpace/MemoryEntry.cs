using HydraForge.Domain.Enums;
using Pgvector;

namespace HydraForge.Domain.Entities.PersonalSpace;

public class MemoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public MemoryCategory Category { get; set; }
    public string Content { get; set; } = string.Empty;
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}