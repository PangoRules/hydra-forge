using HydraForge.Domain.Enums;
using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; } = MemberRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
