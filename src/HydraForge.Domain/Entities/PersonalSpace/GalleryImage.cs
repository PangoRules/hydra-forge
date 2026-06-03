namespace HydraForge.Domain.Entities.PersonalSpace;

public class GalleryImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}