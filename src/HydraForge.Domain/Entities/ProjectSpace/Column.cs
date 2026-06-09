namespace HydraForge.Domain.Entities.ProjectSpace;

public class Column
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public int? WipLimit { get; set; }
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void UpdateDetails(string name, string? color, int? wipLimit)
    {
        Name = name;
        Color = color;
        WipLimit = wipLimit;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignPosition(int position)
    {
        Position = position;
    }
}
