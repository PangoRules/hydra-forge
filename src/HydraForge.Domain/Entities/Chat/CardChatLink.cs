namespace HydraForge.Domain.Entities.Chat;

public class CardChatLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CardId { get; set; }
    public Guid SessionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}