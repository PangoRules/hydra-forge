namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;

public class AgentPersonalityServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    private sealed class FakePersonalityRepo : IAgentPersonalityRepository
    {
        public List<AgentPersonality> Personalities { get; } = [];

        public Task AddAsync(AgentPersonality personality, CancellationToken ct = default)
        {
            Personalities.Add(personality);
            return Task.CompletedTask;
        }

        public Task ArchiveAsync(Guid personalityId, CancellationToken ct = default)
        {
            var p = Personalities.FirstOrDefault(x => x.Id == personalityId);
            if (p != null)
                p.ArchivedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task<AgentPersonality?> GetByIdAsync(
            Guid personalityId,
            CancellationToken ct = default
        ) => Task.FromResult(Personalities.FirstOrDefault(p => p.Id == personalityId));

        public Task<AgentPersonality?> GetDefaultAsync(
            Guid userId,
            CancellationToken ct = default
        ) => Task.FromResult(Personalities.FirstOrDefault(p => p.UserId == userId && p.IsDefault));

        public Task<IReadOnlyList<AgentPersonality>> ListByUserAsync(
            Guid userId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<AgentPersonality>>(
                Personalities.Where(p => p.UserId == userId).ToList()
            );

        public Task SetDefaultAsync(Guid personalityId, Guid userId, CancellationToken ct = default)
        {
            foreach (var p in Personalities.Where(p => p.UserId == userId))
                p.IsDefault = false;
            var target = Personalities.FirstOrDefault(p => p.Id == personalityId);
            if (target != null)
                target.IsDefault = true;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AgentPersonality personality, CancellationToken ct = default)
        {
            var idx = Personalities.FindIndex(p => p.Id == personality.Id);
            if (idx >= 0)
                Personalities[idx] = personality;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CreateAsync_ReturnsDto()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var request = new CreateAgentPersonalityRequest(
            "Test Bot",
            "A test personality",
            "You are helpful.",
            false
        );

        var result = await service.CreateAsync(request, userId);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal("Test Bot", dto.Name);
        Assert.Equal("A test personality", dto.Description);
        Assert.Equal("You are helpful.", dto.SystemPrompt);
        Assert.False(dto.IsDefault);
    }

    [Fact]
    public async Task CreateAsync_WithIsDefaultTrue_ClearsExistingDefault()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();

        var first = new AgentPersonality
        {
            Id = NewId(),
            UserId = userId,
            Name = "First",
            SystemPrompt = "First prompt",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        repo.Personalities.Add(first);

        var request = new CreateAgentPersonalityRequest("Second", null, "Second prompt", true);
        var result = await service.CreateAsync(request, userId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDefault);
        Assert.False(repo.Personalities.First(p => p.Id == first.Id).IsDefault);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ReturnsValidationError()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var request = new CreateAgentPersonalityRequest("   ", null, "prompt", false);

        var result = await service.CreateAsync(request, NewId());

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Validation.Required, result.Error.Code);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyNonArchived()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();

        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = NewId(),
                UserId = userId,
                Name = "Active",
                SystemPrompt = "p",
                ArchivedAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = NewId(),
                UserId = userId,
                Name = "Archived",
                SystemPrompt = "p",
                ArchivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ListAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Active", result.Value[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_ReturnsDto()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = userId,
                Name = "Old Name",
                SystemPrompt = "old prompt",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.UpdateAsync(
            personalityId,
            new UpdateAgentPersonalityRequest("New Name", "New desc", "new prompt"),
            userId
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.Name);
        Assert.Equal("New desc", result.Value.Description);
        Assert.Equal("new prompt", result.Value.SystemPrompt);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsError()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);

        var result = await service.UpdateAsync(
            NewId(),
            new UpdateAgentPersonalityRequest("n", null, null),
            NewId()
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Chat.PersonalityNotFound, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_ReturnsError()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var ownerId = NewId();
        var strangerId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = ownerId,
                Name = "Test",
                SystemPrompt = "p",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.UpdateAsync(
            personalityId,
            new UpdateAgentPersonalityRequest("Hacked", null, null),
            strangerId
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Chat.PersonalityNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_ArchivedPersonality_ReturnsError()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = userId,
                Name = "Archived",
                SystemPrompt = "p",
                ArchivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.UpdateAsync(
            personalityId,
            new UpdateAgentPersonalityRequest("New Name", null, null),
            userId
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Chat.PersonalityNotFound, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_ArchivedNonOwner_ReturnsPersonalityNotOwner()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var ownerId = NewId();
        var strangerId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = ownerId,
                Name = "Archived",
                SystemPrompt = "p",
                ArchivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.UpdateAsync(
            personalityId,
            new UpdateAgentPersonalityRequest("New Name", null, null),
            strangerId
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Chat.PersonalityNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task SetDefaultAsync_ArchivedNonOwner_ReturnsPersonalityNotOwner()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var ownerId = NewId();
        var strangerId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = ownerId,
                Name = "Archived",
                SystemPrompt = "p",
                ArchivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.SetDefaultAsync(personalityId, strangerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Chat.PersonalityNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_WhitespaceName_ReturnsError()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = userId,
                Name = "Original",
                SystemPrompt = "p",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.UpdateAsync(
            personalityId,
            new UpdateAgentPersonalityRequest("   ", null, null),
            userId
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Validation.Required, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_SetsArchivedAt_ReturnsDto()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = userId,
                Name = "Test",
                SystemPrompt = "p",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ArchiveAsync(personalityId, userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_NonOwner_ReturnsError()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var ownerId = NewId();
        var strangerId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = ownerId,
                Name = "Test",
                SystemPrompt = "p",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ArchiveAsync(personalityId, strangerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Chat.PersonalityNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_DoesNotHardDelete()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = userId,
                Name = "Test",
                SystemPrompt = "p",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        await service.ArchiveAsync(personalityId, userId);

        Assert.Single(repo.Personalities);
        Assert.NotNull(repo.Personalities[0].ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_ClearsIsDefault()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = userId,
                Name = "Default Bot",
                SystemPrompt = "p",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        await service.ArchiveAsync(personalityId, userId);

        var archived = repo.Personalities[0];
        Assert.NotNull(archived.ArchivedAt);
        Assert.False(archived.IsDefault);
    }

    [Fact]
    public async Task SetDefaultAsync_SetsDefault_ClearsOthers()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var existingDefaultId = NewId();
        var newDefaultId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = existingDefaultId,
                UserId = userId,
                Name = "Old Default",
                SystemPrompt = "p",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = newDefaultId,
                UserId = userId,
                Name = "New Default",
                SystemPrompt = "p",
                IsDefault = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.SetDefaultAsync(newDefaultId, userId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDefault);
        Assert.False(repo.Personalities.First(p => p.Id == existingDefaultId).IsDefault);
        Assert.True(repo.Personalities.First(p => p.Id == newDefaultId).IsDefault);
    }

    [Fact]
    public async Task SetDefaultAsync_NotFound_ReturnsError()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);

        var result = await service.SetDefaultAsync(NewId(), NewId());

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Chat.PersonalityNotFound, result.Error.Code);
    }

    [Fact]
    public async Task SetDefaultAsync_ArchivedPersonality_ReturnsError()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        var personalityId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = personalityId,
                UserId = userId,
                Name = "Archived Bot",
                SystemPrompt = "p",
                ArchivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.SetDefaultAsync(personalityId, userId);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Chat.PersonalityNotFound, result.Error.Code);
    }

    [Fact]
    public async Task ListAsync_ConcurrentFirstFetches_SeedsOnlyOnce()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();

        var results = await Task.WhenAll(service.ListAsync(userId), service.ListAsync(userId));

        Assert.True(results[0].IsSuccess);
        Assert.True(results[1].IsSuccess);
        Assert.Equal(DefaultAgentPersonalities.All.Count, repo.Personalities.Count(p => p.UserId == userId));
    }

    [Fact]
    public async Task ListAsync_ZeroPersonalitiesEver_SeedsDefaults()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();

        var result = await service.ListAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(DefaultAgentPersonalities.All.Count, result.Value.Count);
        Assert.Equal(DefaultAgentPersonalities.All.Count, repo.Personalities.Count(p => p.UserId == userId));
        Assert.Contains(result.Value, p => p.Name == "The Well");
        Assert.Contains(result.Value, p => p.Name == "Carter");
        Assert.Contains(result.Value, p => p.Name == "Commander Erwin");
        Assert.Contains(result.Value, p => p.Name == "Sokka");
        Assert.Contains(result.Value, p => p.Name == "Captain Levi");
        Assert.Contains(result.Value, p => p.Name == "Fire_Keeper");
        Assert.Contains(result.Value, p => p.Name == "Tarnished");
        Assert.Contains(result.Value, p => p.Name == "Uncle Iroh");
        Assert.Contains(result.Value, p => p.Name == "Hosea Matthews");
        Assert.Contains(result.Value, p => p.Name == "Strelok");
        Assert.Contains(result.Value, p => p.Name == "Mikasa Ackerman");
    }

    [Fact]
    public async Task ListAsync_SeededPersonalities_AreNotMarkedDefault()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);

        var result = await service.ListAsync(NewId());

        Assert.All(result.Value, p => Assert.False(p.IsDefault));
    }

    [Fact]
    public async Task ListAsync_UserAlreadyHasAPersonality_DoesNotReseed()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = NewId(),
                UserId = userId,
                Name = "My Own Bot",
                SystemPrompt = "p",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ListAsync(userId);

        Assert.Single(result.Value);
        Assert.Equal("My Own Bot", result.Value[0].Name);
    }

    [Fact]
    public async Task ListAsync_UserHasOnlyArchivedPersonality_DoesNotReseed()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = NewId(),
                UserId = userId,
                Name = "Archived Bot",
                SystemPrompt = "p",
                ArchivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ListAsync(userId);

        Assert.Empty(result.Value);
        Assert.Single(repo.Personalities);
    }

    [Fact]
    public async Task ListAsync_TwoDifferentUsers_EachGetsTheirOwnSeededDefaults()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userA = NewId();
        var userB = NewId();

        var resultA = await service.ListAsync(userA);
        var resultB = await service.ListAsync(userB);

        Assert.Equal(DefaultAgentPersonalities.All.Count, resultA.Value.Count);
        Assert.Equal(DefaultAgentPersonalities.All.Count, resultB.Value.Count);
        Assert.Equal(DefaultAgentPersonalities.All.Count * 2, repo.Personalities.Count);
    }
}
