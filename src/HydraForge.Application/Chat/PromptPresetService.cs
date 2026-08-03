using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public class PromptPresetService(
    IPromptPresetRepository presetRepo,
    IPromptPresetGroupRepository groupRepo
) : IPromptPresetService
{
    private readonly IPromptPresetRepository _presetRepo = presetRepo;
    private readonly IPromptPresetGroupRepository _groupRepo = groupRepo;

    public async Task<Result<PromptPresetDto>> CreatePresetAsync(
        CreatePromptPresetRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<PromptPresetDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Preset name is required.")
            );

        if (request.GroupId.HasValue)
        {
            var group = await _groupRepo.GetByIdAsync(request.GroupId.Value, ct);
            if (group == null)
                return Result<PromptPresetDto>.Failure(
                    new Error(DomainErrorCodes.Chat.PresetGroupNotFound, "Preset group not found.")
                );
            if (group.UserId != actorId)
                return Result<PromptPresetDto>.Failure(
                    new Error(
                        DomainErrorCodes.Chat.PresetGroupNotOwner,
                        "Only the group owner can add presets to it."
                    )
                );
        }

        var preset = new PromptPreset
        {
            Id = Guid.NewGuid(),
            UserId = actorId,
            GroupId = request.GroupId,
            Name = request.Name,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _presetRepo.AddAsync(preset, ct);
        return Result<PromptPresetDto>.Success(MapToDto(preset));
    }

    public async Task<Result<IReadOnlyList<PromptPresetDto>>> ListPresetsAsync(
        Guid actorId,
        Guid? groupId,
        CancellationToken ct = default
    )
    {
        var all = await _presetRepo.ListByUserAsync(actorId, ct);
        var active = all.Where(p => !p.ArchivedAt.HasValue);

        IReadOnlyList<PromptPresetDto> result;
        if (!groupId.HasValue)
        {
            result = active.Select(MapToDto).ToList();
        }
        else if (groupId.Value == Guid.Empty)
        {
            result = active.Where(p => !p.GroupId.HasValue).Select(MapToDto).ToList();
        }
        else
        {
            result = active.Where(p => p.GroupId == groupId.Value).Select(MapToDto).ToList();
        }

        return Result<IReadOnlyList<PromptPresetDto>>.Success(result);
    }

    public async Task<Result<PromptPresetDto>> UpdatePresetAsync(
        Guid presetId,
        UpdatePromptPresetRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<PromptPresetDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Preset name is required.")
            );

        var preset = await _presetRepo.GetByIdAsync(presetId, ct);
        if (preset == null)
            return Result<PromptPresetDto>.Failure(
                new Error(DomainErrorCodes.Chat.PresetNotFound, "Preset not found.")
            );

        if (preset.UserId != actorId)
            return Result<PromptPresetDto>.Failure(
                new Error(DomainErrorCodes.Chat.PresetNotOwner, "Only the owner can update the preset.")
            );

        if (request.GroupId.HasValue)
        {
            var group = await _groupRepo.GetByIdAsync(request.GroupId.Value, ct);
            if (group == null)
                return Result<PromptPresetDto>.Failure(
                    new Error(DomainErrorCodes.Chat.PresetGroupNotFound, "Preset group not found.")
                );
            if (group.UserId != actorId)
                return Result<PromptPresetDto>.Failure(
                    new Error(
                        DomainErrorCodes.Chat.PresetGroupNotOwner,
                        "Only the group owner can assign the preset to that group."
                    )
                );
        }

        preset.Name = request.Name;
        preset.Content = request.Content;
        preset.GroupId = request.GroupId;
        preset.UpdatedAt = DateTime.UtcNow;

        await _presetRepo.UpdateAsync(preset, ct);
        return Result<PromptPresetDto>.Success(MapToDto(preset));
    }

    public async Task<Result<PromptPresetDto>> ArchivePresetAsync(
        Guid presetId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var preset = await _presetRepo.GetByIdAsync(presetId, ct);
        if (preset == null)
            return Result<PromptPresetDto>.Failure(
                new Error(DomainErrorCodes.Chat.PresetNotFound, "Preset not found.")
            );

        if (preset.UserId != actorId)
            return Result<PromptPresetDto>.Failure(
                new Error(DomainErrorCodes.Chat.PresetNotOwner, "Only the owner can archive the preset.")
            );

        await _presetRepo.ArchiveAsync(presetId, ct);
        preset = await _presetRepo.GetByIdAsync(presetId, ct);
        if (preset == null)
            return Result<PromptPresetDto>.Failure(
                new Error(DomainErrorCodes.Chat.PresetNotFound, "Preset not found.")
            );

        return Result<PromptPresetDto>.Success(MapToDto(preset));
    }

    public async Task<Result<PromptPresetGroupDto>> CreateGroupAsync(
        CreatePromptPresetGroupRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<PromptPresetGroupDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Group name is required.")
            );

        var group = new PromptPresetGroup
        {
            Id = Guid.NewGuid(),
            UserId = actorId,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _groupRepo.AddAsync(group, ct);
        return Result<PromptPresetGroupDto>.Success(MapGroupToDto(group, []));
    }

    public async Task<Result<IReadOnlyList<PromptPresetGroupDto>>> ListGroupsAsync(
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var groups = await _groupRepo.ListByUserAsync(actorId, ct);
        var active = groups.Where(g => !g.ArchivedAt.HasValue);
        var presets = await _presetRepo.ListByUserAsync(actorId, ct);
        var activePresets = presets.Where(p => !p.ArchivedAt.HasValue).ToLookup(p => p.GroupId);

        var dtos = active
            .Select(g => MapGroupToDto(g, activePresets[g.Id].Select(MapToDto).ToList()))
            .ToList();

        return Result<IReadOnlyList<PromptPresetGroupDto>>.Success(dtos);
    }

    public async Task<Result<PromptPresetGroupDto>> UpdateGroupAsync(
        Guid groupId,
        UpdatePromptPresetGroupRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<PromptPresetGroupDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Group name is required.")
            );

        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
            return Result<PromptPresetGroupDto>.Failure(
                new Error(DomainErrorCodes.Chat.PresetGroupNotFound, "Group not found.")
            );

        if (group.UserId != actorId)
            return Result<PromptPresetGroupDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.PresetGroupNotOwner,
                    "Only the owner can update the group."
                )
            );

        group.Name = request.Name;
        group.UpdatedAt = DateTime.UtcNow;

        await _groupRepo.UpdateAsync(group, ct);

        var presets = await _presetRepo.ListByUserAsync(actorId, ct);
        var groupPresets = presets
            .Where(p => p.GroupId == groupId && !p.ArchivedAt.HasValue)
            .Select(MapToDto)
            .ToList();

        return Result<PromptPresetGroupDto>.Success(MapGroupToDto(group, groupPresets));
    }

    public async Task<Result<PromptPresetGroupDto>> ArchiveGroupAsync(
        Guid groupId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
            return Result<PromptPresetGroupDto>.Failure(
                new Error(DomainErrorCodes.Chat.PresetGroupNotFound, "Group not found.")
            );

        if (group.UserId != actorId)
            return Result<PromptPresetGroupDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.PresetGroupNotOwner,
                    "Only the owner can archive the group."
                )
            );

        await _groupRepo.ArchiveAsync(groupId, ct);
        await _presetRepo.NullifyGroupAsync(groupId, ct);

        group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null)
            return Result<PromptPresetGroupDto>.Failure(
                new Error(DomainErrorCodes.Chat.PresetGroupNotFound, "Group not found.")
            );

        var presets = await _presetRepo.ListByUserAsync(actorId, ct);
        var groupPresets = presets
            .Where(p => p.GroupId == groupId && !p.ArchivedAt.HasValue)
            .Select(MapToDto)
            .ToList();

        return Result<PromptPresetGroupDto>.Success(MapGroupToDto(group, groupPresets));
    }

    private static PromptPresetDto MapToDto(PromptPreset p) =>
        new(
            p.Id,
            p.GroupId,
            p.Name,
            p.Content,
            p.CreatedAt,
            p.UpdatedAt,
            p.ArchivedAt
        );

    private static PromptPresetGroupDto MapGroupToDto(
        PromptPresetGroup g,
        IReadOnlyList<PromptPresetDto> presets
    ) =>
        new(
            g.Id,
            g.Name,
            g.CreatedAt,
            g.UpdatedAt,
            g.ArchivedAt,
            presets
        );
}
