using HydraForge.Domain.Enums;

namespace HydraForge.Application.Projects;

// ── Requests ────────────────────────────────────────────────

public record CreateProjectRequest(
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider
);

public record UpdateProjectRequest(
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider
);

public record AddMemberRequest(
    Guid UserId,
    MemberRole Role
);

public record UpdateMemberRequest(
    MemberRole Role
);

// ── Responses ───────────────────────────────────────────────

public record ProjectResponse(
    Guid Id,
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt,
    IReadOnlyList<ColumnResponse> Columns,
    IReadOnlyList<MemberResponse> Members
);

public record ColumnResponse(
    Guid Id,
    string Name,
    int Position,
    int? WipLimit,
    string? Color
);

public record MemberResponse(
    Guid Id,
    Guid UserId,
    MemberRole Role,
    DateTime JoinedAt
);

public record ProjectListResponse(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? ArchivedAt,
    int MemberCount
);