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
    Guid? ForkedFromSessionId
);

public record UpdateChatSessionRequest(
    string Title,
    Guid? FolderId,
    Guid? PersonalityId,
    AiEditMode? AiEditMode,
    bool SearchAllMyDocs
);

public record MoveChatSessionRequest(Guid? FolderId);

// ── Folder Requests / Responses ─────────────────────────────────

public record CreateChatFolderRequest(
    string Name,
    Guid? ParentFolderId,
    Guid? ProjectId
);

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
    DateTime? ArchivedAt
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
    IReadOnlyList<ChatMessageDto> Messages
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
    DateTime? ArchivedAt
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

public record ChatPermissionDto(bool Granted, AiEditMode Mode);
