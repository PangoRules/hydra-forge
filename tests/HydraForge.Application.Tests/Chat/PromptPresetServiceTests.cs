namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;

public class PromptPresetServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    private sealed class FakePresetRepo : IPromptPresetRepository
    {
        public List<PromptPreset> Presets { get; } = [];

        public Task AddAsync(PromptPreset preset, CancellationToken ct = default)
        {
            Presets.Add(preset);
            return Task.CompletedTask;
        }

        public Task ArchiveAsync(Guid presetId, CancellationToken ct = default)
        {
            var p = Presets.FirstOrDefault(x => x.Id == presetId);
            if (p != null)
                p.ArchivedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task<PromptPreset?> GetByIdAsync(Guid presetId, CancellationToken ct = default) =>
            Task.FromResult(Presets.FirstOrDefault(p => p.Id == presetId));

        public Task<IReadOnlyList<PromptPreset>> ListByUserAsync(
            Guid userId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<PromptPreset>>(
                Presets.Where(p => p.UserId == userId).ToList()
            );

        public Task NullifyGroupAsync(Guid groupId, CancellationToken ct = default)
        {
            foreach (var p in Presets.Where(p => p.GroupId == groupId))
                p.GroupId = null;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PromptPreset preset, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeGroupRepo : IPromptPresetGroupRepository
    {
        public List<PromptPresetGroup> Groups { get; } = [];

        public Task AddAsync(PromptPresetGroup group, CancellationToken ct = default)
        {
            Groups.Add(group);
            return Task.CompletedTask;
        }

        public Task ArchiveAsync(Guid groupId, CancellationToken ct = default)
        {
            var g = Groups.FirstOrDefault(x => x.Id == groupId);
            if (g != null)
                g.ArchivedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task<PromptPresetGroup?> GetByIdAsync(
            Guid groupId,
            CancellationToken ct = default
        ) => Task.FromResult(Groups.FirstOrDefault(g => g.Id == groupId));

        public Task<IReadOnlyList<PromptPresetGroup>> ListByUserAsync(
            Guid userId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<PromptPresetGroup>>(
                Groups.Where(g => g.UserId == userId).ToList()
            );

        public Task UpdateAsync(PromptPresetGroup group, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static (
        PromptPresetService service,
        FakePresetRepo presetRepo,
        FakeGroupRepo groupRepo
    ) CreateSut()
    {
        var presetRepo = new FakePresetRepo();
        var groupRepo = new FakeGroupRepo();
        var service = new PromptPresetService(presetRepo, groupRepo);
        return (service, presetRepo, groupRepo);
    }

    // ── Preset Create ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePresetAsync_Valid_CreatesPreset()
    {
        var (service, presetRepo, _) = CreateSut();
        var actorId = NewId();

        var result = await service.CreatePresetAsync(
            new CreatePromptPresetRequest("My Preset", "Hello {{name}}", null),
            actorId
        );

        Assert.True(result.IsSuccess);
        Assert.Single(presetRepo.Presets);
        Assert.Equal("My Preset", presetRepo.Presets[0].Name);
        Assert.Equal(actorId, presetRepo.Presets[0].UserId);
    }

    [Fact]
    public async Task CreatePresetAsync_WithGroup_ValidatesGroupExists()
    {
        var (service, _, groupRepo) = CreateSut();
        var actorId = NewId();
        var groupId = NewId();
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = groupId,
                UserId = actorId,
                Name = "Group",
            }
        );

        var result = await service.CreatePresetAsync(
            new CreatePromptPresetRequest("P", "C", groupId),
            actorId
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreatePresetAsync_WithNonExistentGroup_ReturnsError()
    {
        var (service, _, _) = CreateSut();

        var result = await service.CreatePresetAsync(
            new CreatePromptPresetRequest("P", "C", NewId()),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetGroupNotFound, result.Error.Code);
    }

    [Fact]
    public async Task CreatePresetAsync_WithOtherUsersGroup_ReturnsError()
    {
        var (service, _, groupRepo) = CreateSut();
        var ownerId = NewId();
        var otherId = NewId();
        var groupId = NewId();
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = groupId,
                UserId = ownerId,
                Name = "Group",
            }
        );

        var result = await service.CreatePresetAsync(
            new CreatePromptPresetRequest("P", "C", groupId),
            otherId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetGroupNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task CreatePresetAsync_EmptyName_ReturnsError()
    {
        var (service, _, _) = CreateSut();

        var result = await service.CreatePresetAsync(
            new CreatePromptPresetRequest("", "C", null),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Validation.Required, result.Error.Code);
    }

    // ── Preset List ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListPresetsAsync_NoGroupId_ReturnsAll()
    {
        var (service, presetRepo, _) = CreateSut();
        var actorId = NewId();
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "P1",
            }
        );
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "P2",
                GroupId = NewId(),
            }
        );
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = NewId(),
                Name = "Other",
            }
        );

        var result = await service.ListPresetsAsync(actorId, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task ListPresetsAsync_EmptyGuid_ReturnsUngroupedOnly()
    {
        var (service, presetRepo, _) = CreateSut();
        var actorId = NewId();
        var groupId = NewId();
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "Ungrouped",
            }
        );
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "InGroup",
                GroupId = groupId,
            }
        );

        var result = await service.ListPresetsAsync(actorId, Guid.Empty);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Ungrouped", result.Value[0].Name);
    }

    [Fact]
    public async Task ListPresetsAsync_WithGroupId_ReturnsGroupPresets()
    {
        var (service, presetRepo, _) = CreateSut();
        var actorId = NewId();
        var groupId = NewId();
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "Ungrouped",
            }
        );
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "InGroup",
                GroupId = groupId,
            }
        );

        var result = await service.ListPresetsAsync(actorId, groupId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("InGroup", result.Value[0].Name);
    }

    // ── Get Preset By Id ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPresetByIdAsync_Owner_ReturnsPreset()
    {
        var (service, presetRepo, _) = CreateSut();
        var actorId = NewId();
        var preset = new PromptPreset
        {
            Id = NewId(),
            UserId = actorId,
            Name = "P1",
        };
        presetRepo.Presets.Add(preset);

        var result = await service.GetPresetByIdAsync(preset.Id, actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(preset.Name, result.Value.Name);
    }

    [Fact]
    public async Task GetPresetByIdAsync_NotOwner_ReturnsNotFound()
    {
        var (service, presetRepo, _) = CreateSut();
        var preset = new PromptPreset
        {
            Id = NewId(),
            UserId = NewId(),
            Name = "P1",
        };
        presetRepo.Presets.Add(preset);

        var result = await service.GetPresetByIdAsync(preset.Id, NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task GetPresetByIdAsync_Missing_ReturnsNotFound()
    {
        var (service, _, _) = CreateSut();

        var result = await service.GetPresetByIdAsync(NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetNotFound, result.Error.Code);
    }

    [Fact]
    public async Task ListPresetsAsync_ArchivedExcluded()
    {
        var (service, presetRepo, _) = CreateSut();
        var actorId = NewId();
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "Active",
            }
        );
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "Archived",
                ArchivedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ListPresetsAsync(actorId, null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Active", result.Value[0].Name);
    }

    // ── Preset Update ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePresetAsync_Valid_UpdatesPreset()
    {
        var (service, _, _) = CreateSut();
        var actorId = NewId();
        var preset = new PromptPreset
        {
            Id = NewId(),
            UserId = actorId,
            Name = "Old",
            Content = "OldC",
        };
        var presetRepo = new FakePresetRepo();
        presetRepo.Presets.Add(preset);
        var groupRepo = new FakeGroupRepo();
        var svc = new PromptPresetService(presetRepo, groupRepo);

        var result = await svc.UpdatePresetAsync(
            preset.Id,
            new UpdatePromptPresetRequest("New", "NewC", null, Position: 0),
            actorId
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value.Name);
        Assert.Equal("NewC", result.Value.Content);
    }

    [Fact]
    public async Task UpdatePresetAsync_NotOwner_ReturnsError()
    {
        var (service, _, _) = CreateSut();
        var ownerId = NewId();
        var otherId = NewId();
        var preset = new PromptPreset
        {
            Id = NewId(),
            UserId = ownerId,
            Name = "Old",
        };
        var presetRepo = new FakePresetRepo();
        presetRepo.Presets.Add(preset);
        var groupRepo = new FakeGroupRepo();
        var svc = new PromptPresetService(presetRepo, groupRepo);

        var result = await svc.UpdatePresetAsync(
            preset.Id,
            new UpdatePromptPresetRequest("New", "NewC", null, Position: 0),
            otherId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task UpdatePresetAsync_NotFound_ReturnsError()
    {
        var (service, _, _) = CreateSut();

        var result = await service.UpdatePresetAsync(
            NewId(),
            new UpdatePromptPresetRequest("New", "NewC", null, Position: 0),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetNotFound, result.Error.Code);
    }

    [Fact]
    public async Task UpdatePresetAsync_AssignToOtherUsersGroup_ReturnsError()
    {
        var (service, _, _) = CreateSut();
        var actorId = NewId();
        var otherOwnerId = NewId();
        var groupId = NewId();
        var preset = new PromptPreset
        {
            Id = NewId(),
            UserId = actorId,
            Name = "P",
        };
        var presetRepo = new FakePresetRepo();
        presetRepo.Presets.Add(preset);
        var groupRepo = new FakeGroupRepo();
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = groupId,
                UserId = otherOwnerId,
                Name = "G",
            }
        );
        var svc = new PromptPresetService(presetRepo, groupRepo);

        var result = await svc.UpdatePresetAsync(
            preset.Id,
            new UpdatePromptPresetRequest("P", "C", groupId, Position: 0),
            actorId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetGroupNotOwner, result.Error.Code);
    }

    // ── Preset Archive ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ArchivePresetAsync_Valid_ArchivesPreset()
    {
        var (service, _, _) = CreateSut();
        var actorId = NewId();
        var preset = new PromptPreset
        {
            Id = NewId(),
            UserId = actorId,
            Name = "P",
        };
        var presetRepo = new FakePresetRepo();
        presetRepo.Presets.Add(preset);
        var groupRepo = new FakeGroupRepo();
        var svc = new PromptPresetService(presetRepo, groupRepo);

        var result = await svc.ArchivePresetAsync(preset.Id, actorId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ArchivedAt);
    }

    [Fact]
    public async Task ArchivePresetAsync_NotOwner_ReturnsError()
    {
        var (service, _, _) = CreateSut();
        var ownerId = NewId();
        var otherId = NewId();
        var preset = new PromptPreset
        {
            Id = NewId(),
            UserId = ownerId,
            Name = "P",
        };
        var presetRepo = new FakePresetRepo();
        presetRepo.Presets.Add(preset);
        var groupRepo = new FakeGroupRepo();
        var svc = new PromptPresetService(presetRepo, groupRepo);

        var result = await svc.ArchivePresetAsync(preset.Id, otherId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetNotOwner, result.Error.Code);
    }

    // ── Group Create ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGroupAsync_Valid_CreatesGroup()
    {
        var (service, _, groupRepo) = CreateSut();
        var actorId = NewId();

        var result = await service.CreateGroupAsync(
            new CreatePromptPresetGroupRequest("My Group"),
            actorId
        );

        Assert.True(result.IsSuccess);
        Assert.Single(groupRepo.Groups);
        Assert.Equal("My Group", groupRepo.Groups[0].Name);
    }

    [Fact]
    public async Task CreateGroupAsync_EmptyName_ReturnsError()
    {
        var (service, _, _) = CreateSut();

        var result = await service.CreateGroupAsync(
            new CreatePromptPresetGroupRequest(""),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Validation.Required, result.Error.Code);
    }

    // ── Group List ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListGroupsAsync_ReturnsActiveGroupsWithPresets()
    {
        var (service, presetRepo, groupRepo) = CreateSut();
        var actorId = NewId();
        var groupId = NewId();
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = groupId,
                UserId = actorId,
                Name = "G1",
            }
        );
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = NewId(),
                UserId = actorId,
                Name = "Archived",
                ArchivedAt = DateTime.UtcNow,
            }
        );
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = NewId(),
                UserId = NewId(),
                Name = "Other",
            }
        );
        presetRepo.Presets.Add(
            new PromptPreset
            {
                Id = NewId(),
                UserId = actorId,
                Name = "P1",
                GroupId = groupId,
            }
        );

        var result = await service.ListGroupsAsync(actorId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("G1", result.Value[0].Name);
        Assert.Single(result.Value[0].Presets);
        Assert.Equal("P1", result.Value[0].Presets[0].Name);
    }

    // ── Group Update ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateGroupAsync_Valid_UpdatesGroup()
    {
        var (service, _, _) = CreateSut();
        var actorId = NewId();
        var group = new PromptPresetGroup
        {
            Id = NewId(),
            UserId = actorId,
            Name = "Old",
        };
        var presetRepo = new FakePresetRepo();
        var groupRepo = new FakeGroupRepo();
        groupRepo.Groups.Add(group);
        var svc = new PromptPresetService(presetRepo, groupRepo);

        var result = await svc.UpdateGroupAsync(
            group.Id,
            new UpdatePromptPresetGroupRequest("New"),
            actorId
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value.Name);
    }

    [Fact]
    public async Task UpdateGroupAsync_NotOwner_ReturnsError()
    {
        var (service, _, _) = CreateSut();
        var ownerId = NewId();
        var otherId = NewId();
        var group = new PromptPresetGroup
        {
            Id = NewId(),
            UserId = ownerId,
            Name = "G",
        };
        var presetRepo = new FakePresetRepo();
        var groupRepo = new FakeGroupRepo();
        groupRepo.Groups.Add(group);
        var svc = new PromptPresetService(presetRepo, groupRepo);

        var result = await svc.UpdateGroupAsync(
            group.Id,
            new UpdatePromptPresetGroupRequest("New"),
            otherId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetGroupNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task UpdateGroupAsync_NotFound_ReturnsError()
    {
        var (service, _, _) = CreateSut();

        var result = await service.UpdateGroupAsync(
            NewId(),
            new UpdatePromptPresetGroupRequest("New"),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetGroupNotFound, result.Error.Code);
    }

    // ── Group Archive ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveGroupAsync_SoftArchivesGroup_AndNullsPresetGroupIds()
    {
        var (service, presetRepo, groupRepo) = CreateSut();
        var actorId = NewId();
        var groupId = NewId();
        var preset1 = new PromptPreset
        {
            Id = NewId(),
            UserId = actorId,
            Name = "P1",
            GroupId = groupId,
        };
        var preset2 = new PromptPreset
        {
            Id = NewId(),
            UserId = actorId,
            Name = "P2",
            GroupId = groupId,
        };
        presetRepo.Presets.Add(preset1);
        presetRepo.Presets.Add(preset2);
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = groupId,
                UserId = actorId,
                Name = "G",
            }
        );

        var result = await service.ArchiveGroupAsync(groupId, actorId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ArchivedAt);
        Assert.Null(preset1.GroupId);
        Assert.Null(preset2.GroupId);
    }

    [Fact]
    public async Task ArchiveGroupAsync_NotOwner_ReturnsError()
    {
        var (service, _, groupRepo) = CreateSut();
        var ownerId = NewId();
        var otherId = NewId();
        var groupId = NewId();
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = groupId,
                UserId = ownerId,
                Name = "G",
            }
        );

        var result = await service.ArchiveGroupAsync(groupId, otherId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetGroupNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveGroupAsync_NotFound_ReturnsError()
    {
        var (service, _, _) = CreateSut();

        var result = await service.ArchiveGroupAsync(NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.PresetGroupNotFound, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveGroupAsync_PresetsAreNotDeleted_JustUngrouped()
    {
        var (service, presetRepo, groupRepo) = CreateSut();
        var actorId = NewId();
        var groupId = NewId();
        var preset = new PromptPreset
        {
            Id = NewId(),
            UserId = actorId,
            Name = "P",
            GroupId = groupId,
        };
        presetRepo.Presets.Add(preset);
        groupRepo.Groups.Add(
            new PromptPresetGroup
            {
                Id = groupId,
                UserId = actorId,
                Name = "G",
            }
        );

        await service.ArchiveGroupAsync(groupId, actorId);

        Assert.Single(presetRepo.Presets);
        Assert.Null(preset.ArchivedAt);
        Assert.Null(preset.GroupId);
    }
}
