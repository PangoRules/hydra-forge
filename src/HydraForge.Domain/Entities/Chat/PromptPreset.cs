namespace HydraForge.Domain.Entities.Chat;

public class PromptPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public void Update(string name, string content, Guid? groupId)
    {
        Name = name;
        Content = content;
        GroupId = groupId;
        UpdatedAt = DateTime.UtcNow;
    }
}
