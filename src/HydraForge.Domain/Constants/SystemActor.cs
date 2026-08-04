namespace HydraForge.Domain.Constants;

/// <summary>
/// Well-known actor id for audit entries written by background jobs with no human actor
/// (e.g. housekeeping). <see cref="Entities.ProjectSpace.AuditLogEntry.Create"/> rejects
/// <see cref="Guid.Empty"/>, so batch jobs need a distinct, non-empty, reserved id instead.
/// </summary>
public static class SystemActor
{
    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000002");
}
