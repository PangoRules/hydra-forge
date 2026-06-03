namespace HydraForge.Domain.Entities.Chat;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? FolderId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}