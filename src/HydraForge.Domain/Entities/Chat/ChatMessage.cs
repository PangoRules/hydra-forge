using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.Chat;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokens { get; set; }
    public string? ModelName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
