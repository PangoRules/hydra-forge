using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Chat;

// ── Requests ───────────────────────────────────────────────────

public record CreateChatSessionRequest(
    string Title,
    Guid? FolderId,
    Guid? ProjectId,
    Guid? OpenCardId,
    Guid? PersonalityId,
    AiEditMode? AiEditMode,
    bool SearchAllMyDocs,
    Guid? ForkedFromSessionId,
    Guid? PreferredModelConfigId,
    string? PreferredEffort
);

public record UpdateChatSessionRequest(
    string Title,
    Guid? FolderId,
    Guid? PersonalityId,
    AiEditMode? AiEditMode,
    bool SearchAllMyDocs,
    Guid? PreferredModelConfigId,
    string? PreferredEffort
);

public record MoveChatSessionRequest(Guid? FolderId);

// ── Enums ───────────────────────────────────────────────────────

public enum ChatSessionScope
{
    Mine,
    Participated,
}

public enum ChatSessionKind
{
    Normal,
    Project,
    Card,
}

public enum ChatSessionStatusFilter
{
    NonArchived,
    Active,
    Closed,
    Archived,
}

// ── Folder Requests / Responses ─────────────────────────────────

public record CreateChatFolderRequest(string Name, Guid? ParentFolderId, Guid? ProjectId);

public record UpdateChatFolderRequest(string Name, Guid? ParentFolderId);

// ── Responses ─────────────────────────────────────────────────

public record ChatSessionDto(
    Guid Id,
    string Title,
    Guid? FolderId,
    Guid? ProjectId,
    Guid? OpenCardId,
    Guid? PersonalityId,
    bool PersonalityArchived,
    ChatSessionStatus Status,
    AiEditMode AiEditMode,
    bool SearchAllMyDocs,
    string? Summary,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt,
    Guid? PreferredModelConfigId,
    string? PreferredEffort
);

public record ChatSessionDetailDto(
    Guid Id,
    string Title,
    Guid? FolderId,
    Guid OwnerId,
    Guid? ProjectId,
    Guid? OpenCardId,
    Guid? PersonalityId,
    bool PersonalityArchived,
    bool IsShared,
    ChatSessionStatus Status,
    AiEditMode AiEditMode,
    bool SearchAllMyDocs,
    string? Summary,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt,
    DateTime? ClosedAt,
    IReadOnlyList<ChatMessageDto> Messages,
    Guid? PreferredModelConfigId,
    string? PreferredEffort
);

public record ChatSessionPageDto(IReadOnlyList<ChatSessionDto> Items, int TotalCount);

public record ChatMessageDto(
    Guid Id,
    Guid SessionId,
    MessageRole Role,
    string Content,
    int InputTokens,
    int OutputTokens,
    int CachedTokens,
    decimal? Cost,
    string? ModelName,
    string? ImagesJson,
    DateTime CreatedAt
);

public record ChatMessagePageDto(IReadOnlyList<ChatMessageDto> Items, int TotalCount);

public record ChatFolderDto(
    Guid Id,
    string Name,
    Guid? ParentFolderId,
    Guid? ProjectId,
    DateTime CreatedAt,
    DateTime? ArchivedAt
);

public record PromptPresetDto(
    Guid Id,
    Guid? GroupId,
    string Name,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt,
    int Position = 0
);

public record PromptPresetGroupDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt,
    IReadOnlyList<PromptPresetDto> Presets
);

public record AgentPersonalityDto(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt
);

public record CardChatLinkDto(
    Guid Id,
    Guid CardId,
    Guid ChatSessionId,
    Guid OwnerId,
    string OwnerUsername,
    string Summary,
    DateTime CreatedAt,
    DateTime? ArchivedAt
);

public record DocumentDto(
    Guid Id,
    string Title,
    string ContentType,
    string? Language,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt
);

public record ChatSearchResultDto(
    Guid SessionId,
    string SessionTitle,
    string MatchedOn,
    string? Snippet
);

public record ChatSessionDocumentDto(
    Guid Id,
    Guid SessionId,
    Guid DocumentId,
    Guid AddedByUserId,
    DateTime AddedAt,
    string Title,
    string ContentType,
    string? Language,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ChatPermissionDto(bool Granted, AiEditMode Mode);
