namespace HydraForge.Domain.Entities.ProjectSpace;

public class CardWatcher
{
    public Guid CardId { get; set; }
    public Guid UserId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
