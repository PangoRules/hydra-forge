using HydraForge.Domain.Common;

namespace HydraForge.Application.Chat;

public record CreatePromptPresetRequest(string Name, string Content, Guid? GroupId);
public record UpdatePromptPresetRequest(string Name, string Content, Guid? GroupId);
public record CreatePromptPresetGroupRequest(string Name);
public record UpdatePromptPresetGroupRequest(string Name);

public interface IPromptPresetService
{
    Task<Result<PromptPresetDto>> CreatePresetAsync(
        CreatePromptPresetRequest request,
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<IReadOnlyList<PromptPresetDto>>> ListPresetsAsync(
        Guid actorId,
        Guid? groupId,
        CancellationToken ct = default
    );

    Task<Result<PromptPresetDto>> UpdatePresetAsync(
        Guid presetId,
        UpdatePromptPresetRequest request,
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<PromptPresetDto>> ArchivePresetAsync(
        Guid presetId,
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<PromptPresetGroupDto>> CreateGroupAsync(
        CreatePromptPresetGroupRequest request,
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<IReadOnlyList<PromptPresetGroupDto>>> ListGroupsAsync(
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<PromptPresetGroupDto>> UpdateGroupAsync(
        Guid groupId,
        UpdatePromptPresetGroupRequest request,
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<PromptPresetGroupDto>> ArchiveGroupAsync(
        Guid groupId,
        Guid actorId,
        CancellationToken ct = default
    );
}
