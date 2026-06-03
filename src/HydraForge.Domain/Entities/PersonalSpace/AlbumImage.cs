namespace HydraForge.Domain.Entities.PersonalSpace;

public class AlbumImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlbumId { get; set; }
    public Guid ImageId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}