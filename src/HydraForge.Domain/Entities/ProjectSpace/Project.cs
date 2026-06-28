namespace HydraForge.Domain.Entities.ProjectSpace;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? GitRemoteUrl { get; set; }
    public string? GitProvider { get; set; }
    public List<Column> Columns { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public void UpdateDetails(string name, string description, string? gitRemoteUrl, string? gitProvider)
    {
        Name = name;
        Description = description;
        GitRemoteUrl = gitRemoteUrl;
        GitProvider = gitProvider;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        ArchivedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        ArchivedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
