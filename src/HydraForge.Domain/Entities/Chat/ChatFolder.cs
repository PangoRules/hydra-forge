namespace HydraForge.Domain.Entities.Chat;

public class ChatFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public void Rename(string name, Guid? parentFolderId)
    {
        Name = name;
        ParentFolderId = parentFolderId;
    }

    public void Archive() => ArchivedAt = DateTime.UtcNow;
}
