using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.PersonalSpace;

public class ImageTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImageId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public TagSource Source { get; set; } = TagSource.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
