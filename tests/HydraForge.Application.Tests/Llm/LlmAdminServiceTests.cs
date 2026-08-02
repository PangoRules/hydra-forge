namespace HydraForge.Application.Tests.Llm;

using HydraForge.Application.Auth;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Enums;
using NSubstitute;

public class LlmAdminServiceTests
{
    private static LlmAdminService CreateService(
        ILlmAdminRepository repo,
        ILlmClientFactory? factory = null,
        IUserRepository? userRepo = null
    )
    {
        var users = userRepo ?? Substitute.For<IUserRepository>();
        if (userRepo is null)
        {
            users
                .FindByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new Dictionary<Guid, User>());
        }

        return new(
            repo,
            Substitute.For<IKeyVault>(),
            factory ?? Substitute.For<ILlmClientFactory>(),
            users
        );
    }

    [Fact]
    public async Task CreateModelAsync_DuplicateModelId_ReturnsModelAlreadyExists()
    {
        var providerId = Guid.NewGuid();
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.GetProviderByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(new LlmProvider { Id = providerId, Name = "Local Ollama" });
        repo.ListModelConfigsAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(
                new List<ProviderModelConfig>
                {
                    new()
                    {
                        ProviderId = providerId,
                        ModelId = "qwen3-coder:latest",
                        Name = "qwen3-coder:latest",
                    },
                }
            );

        var service = CreateService(repo);
        var input = new CreateModelInput("qwen3-coder:latest", "dup", "Standard", null, null, true);

        var result = await service.CreateModelAsync(providerId, input, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.ModelAlreadyExists, result.Error.Code);
        await repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateModelAsync_NewModelId_Succeeds()
    {
        var providerId = Guid.NewGuid();
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.GetProviderByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(new LlmProvider { Id = providerId, Name = "Local Ollama" });
        repo.ListModelConfigsAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(new List<ProviderModelConfig>());

        var service = CreateService(repo);
        var input = new CreateModelInput("qwen3-coder:latest", "New", "Standard", null, null, true);

        var result = await service.CreateModelAsync(providerId, input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("qwen3-coder:latest", result.Value.ModelId);
        repo.Received(1).AddModelConfig(Arg.Any<ProviderModelConfig>());
    }

    [Fact]
    public async Task QueryTokenUsageAsync_MultipleFeatures_ParsesAllAndPassesToRepo()
    {
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.QueryTokenUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<AiFeature>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<TokenUsageRecord>(), 0, 0L, 0L, 0m));

        var service = CreateService(repo);

        var result = await service.QueryTokenUsageAsync(
            null,
            null,
            ["PersonalChat", "ProjectChat"],
            null,
            null,
            null,
            null,
            0,
            50,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        await repo.Received(1)
            .QueryTokenUsageAsync(
                null,
                null,
                Arg.Is<IReadOnlyList<AiFeature>?>(f =>
                    f != null
                    && f.Count == 2
                    && f.Contains(AiFeature.PersonalChat)
                    && f.Contains(AiFeature.ProjectChat)
                ),
                null,
                null,
                null,
                null,
                0,
                50,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task QueryTokenUsageAsync_UnknownFeature_ReturnsInvalidFeatureFailure()
    {
        var repo = Substitute.For<ILlmAdminRepository>();
        var service = CreateService(repo);

        var result = await service.QueryTokenUsageAsync(
            null,
            null,
            ["PersonalChat", "NotARealFeature"],
            null,
            null,
            null,
            null,
            0,
            50,
            CancellationToken.None
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.InvalidFeature, result.Error.Code);
        await repo.DidNotReceive()
            .QueryTokenUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<AiFeature>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task QueryTokenUsageAsync_NoFeatures_PassesNullToRepo()
    {
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.QueryTokenUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<AiFeature>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<TokenUsageRecord>(), 0, 0L, 0L, 0m));

        var service = CreateService(repo);

        var result = await service.QueryTokenUsageAsync(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            50,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        await repo.Received(1)
            .QueryTokenUsageAsync(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                50,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task QueryImageUsageAsync_MultipleFeatures_ParsesAllAndPassesToRepo()
    {
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.QueryImageUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<AiFeature>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<ImageUsageRecord>(), 0, 0, 0m));

        var service = CreateService(repo);

        var result = await service.QueryImageUsageAsync(
            null,
            null,
            ["ImageChat", "ImageDocument"],
            null,
            null,
            null,
            null,
            0,
            50,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        await repo.Received(1)
            .QueryImageUsageAsync(
                null,
                null,
                Arg.Is<IReadOnlyList<AiFeature>?>(f =>
                    f != null
                    && f.Count == 2
                    && f.Contains(AiFeature.ImageChat)
                    && f.Contains(AiFeature.ImageDocument)
                ),
                null,
                null,
                null,
                null,
                0,
                50,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task QueryTokenUsageAsync_ResolvesUserNamesForReturnedRecords()
    {
        var userId = Guid.NewGuid();
        var user = User.Create("testuser1", "Test", "User", "testuser1@localhost", "hash");
        var repo = Substitute.For<ILlmAdminRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        userRepo
            .FindByIdsAsync(
                Arg.Is<IReadOnlyList<Guid>>(ids => ids.Contains(userId)),
                Arg.Any<CancellationToken>()
            )
            .Returns(new Dictionary<Guid, User> { [userId] = user });

        var record = new TokenUsageRecord { UserId = userId, ModelName = "gpt-5" };
        repo.QueryTokenUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<AiFeature>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<TokenUsageRecord> { record }, 1, 0L, 0L, 0m));

        var service = CreateService(repo, userRepo: userRepo);

        var result = await service.QueryTokenUsageAsync(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            50,
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("testuser1", result.Value.Items[0].UserName);
    }

    [Fact]
    public async Task QueryImageUsageAsync_UnknownFeature_ReturnsInvalidFeatureFailure()
    {
        var repo = Substitute.For<ILlmAdminRepository>();
        var service = CreateService(repo);

        var result = await service.QueryImageUsageAsync(
            null,
            null,
            ["NotARealFeature"],
            null,
            null,
            null,
            null,
            0,
            50,
            CancellationToken.None
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.InvalidFeature, result.Error.Code);
    }

    [Fact]
    public async Task GetAccountUsageAsync_MergesTokenAndImageCalls_SortsByTimestampDescendingLimitedTo20()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<ILlmAdminRepository>();
        var budget = new UserTokenBudget
        {
            UserId = userId,
            MonthlyTokenBudget = 100_000,
            MonthlyImageBudget = 50,
            PeriodStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        repo.GetBudgetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(budget);

        var older = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);

        var tokenRecords = new List<TokenUsageRecord>
        {
            new()
            {
                Feature = AiFeature.PersonalChat,
                ModelName = "gpt-5",
                InputTokens = 100,
                OutputTokens = 50,
                Cost = 0.01m,
                CreatedAt = older,
            },
        };
        var imageRecords = new List<ImageUsageRecord>
        {
            new()
            {
                Feature = AiFeature.ImageChat,
                ModelName = "dall-e-3",
                ImageCount = 2,
                Cost = 0.08m,
                CreatedAt = newer,
            },
        };

        repo.QueryTokenUsageAsync(
                userId,
                null,
                null,
                null,
                null,
                budget.PeriodStart,
                budget.PeriodEnd,
                0,
                20,
                Arg.Any<CancellationToken>()
            )
            .Returns((tokenRecords, tokenRecords.Count, 100L, 50L, 0.01m));
        repo.QueryImageUsageAsync(
                userId,
                null,
                null,
                null,
                null,
                budget.PeriodStart,
                budget.PeriodEnd,
                0,
                20,
                Arg.Any<CancellationToken>()
            )
            .Returns((imageRecords, imageRecords.Count, 2, 0.08m));

        var service = CreateService(repo);

        var result = await service.GetAccountUsageAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(150, result.Value.TokensUsed);
        Assert.Equal(2, result.Value.ImagesUsed);
        Assert.Equal(2, result.Value.RecentCalls.Count);
        Assert.Equal("ImageChat", result.Value.RecentCalls[0].Feature);
        Assert.Equal("PersonalChat", result.Value.RecentCalls[1].Feature);
    }

    [Fact]
    public async Task GetAccountUsageAsync_NoBudgetRecord_DefaultsBudgetsToZero()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.GetBudgetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((UserTokenBudget?)null);
        repo.QueryTokenUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<AiFeature>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<TokenUsageRecord>(), 0, 0L, 0L, 0m));
        repo.QueryImageUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<AiFeature>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<ImageUsageRecord>(), 0, 0, 0m));

        var service = CreateService(repo);

        var result = await service.GetAccountUsageAsync(userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TokensBudget);
        Assert.Equal(0, result.Value.ImagesBudget);
        Assert.Empty(result.Value.RecentCalls);
    }

    [Fact]
    public async Task SetAllowedModelsAsync_UnknownFeature_ReturnsInvalidFeature()
    {
        var repo = Substitute.For<ILlmAdminRepository>();
        var service = CreateService(repo);

        var result = await service.SetAllowedModelsAsync(
            "NotARealFeature",
            new SetAllowedModelsInput(Array.Empty<Guid>()),
            CancellationToken.None
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.InvalidFeature, result.Error.Code);
    }

    [Fact]
    public async Task SetAllowedModelsAsync_RoutingNotConfigured_ReturnsRoutingNotFound()
    {
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.GetRoutingByFeatureAsync(AiFeature.PersonalChat, Arg.Any<CancellationToken>())
            .Returns((FeatureRoutingConfig?)null);

        var service = CreateService(repo);

        var result = await service.SetAllowedModelsAsync(
            "PersonalChat",
            new SetAllowedModelsInput(Array.Empty<Guid>()),
            CancellationToken.None
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.RoutingNotFound, result.Error.Code);
    }

    [Fact]
    public async Task SetAllowedModelsAsync_UnknownModelConfigId_ReturnsModelNotFound()
    {
        var configId = Guid.NewGuid();
        var unknownModelId = Guid.NewGuid();
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.GetRoutingByFeatureAsync(AiFeature.PersonalChat, Arg.Any<CancellationToken>())
            .Returns(new FeatureRoutingConfig { Id = configId, Feature = AiFeature.PersonalChat });
        repo.GetModelConfigByIdAsync(unknownModelId, Arg.Any<CancellationToken>())
            .Returns((ProviderModelConfig?)null);

        var service = CreateService(repo);

        var result = await service.SetAllowedModelsAsync(
            "PersonalChat",
            new SetAllowedModelsInput(new[] { unknownModelId }),
            CancellationToken.None
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.ModelNotFound, result.Error.Code);
        await repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAllowedModelsAsync_ValidIds_ReplacesExistingAndSetsPriorityByOrder()
    {
        var configId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var oldAllowedModel = new FeatureAllowedModel
        {
            Id = Guid.NewGuid(),
            FeatureRoutingConfigId = configId,
            ProviderModelConfigId = Guid.NewGuid(),
            Priority = 0,
        };
        var firstModelId = Guid.NewGuid();
        var secondModelId = Guid.NewGuid();
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.GetRoutingByFeatureAsync(AiFeature.PersonalChat, Arg.Any<CancellationToken>())
            .Returns(new FeatureRoutingConfig { Id = configId, Feature = AiFeature.PersonalChat });
        repo.GetModelConfigByIdAsync(firstModelId, Arg.Any<CancellationToken>())
            .Returns(new ProviderModelConfig { Id = firstModelId, ProviderId = providerId });
        repo.GetModelConfigByIdAsync(secondModelId, Arg.Any<CancellationToken>())
            .Returns(new ProviderModelConfig { Id = secondModelId, ProviderId = providerId });
        repo.ListAllowedModelsByFeatureAsync(configId, Arg.Any<CancellationToken>())
            .Returns(new List<FeatureAllowedModel> { oldAllowedModel });

        var service = CreateService(repo);

        var result = await service.SetAllowedModelsAsync(
            "PersonalChat",
            new SetAllowedModelsInput(new[] { firstModelId, secondModelId }),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { firstModelId, secondModelId }, result.Value.AllowedModelConfigIds);
        repo.Received(1).RemoveAllowedModel(oldAllowedModel);
        repo.Received(1)
            .AddAllowedModel(
                Arg.Is<FeatureAllowedModel>(m =>
                    m.ProviderModelConfigId == firstModelId
                    && m.Priority == 0
                    && m.FeatureRoutingConfigId == configId
                )
            );
        repo.Received(1)
            .AddAllowedModel(
                Arg.Is<FeatureAllowedModel>(m =>
                    m.ProviderModelConfigId == secondModelId
                    && m.Priority == 1
                    && m.FeatureRoutingConfigId == configId
                )
            );
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
