namespace HydraForge.Domain.Entities.PersonalSpace;

public class GalleryImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFilename { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public string? ThumbnailPath { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime? TakenAt { get; set; }
    public string? CameraModel { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}
