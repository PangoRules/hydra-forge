using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; } = MemberRole.Viewer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}