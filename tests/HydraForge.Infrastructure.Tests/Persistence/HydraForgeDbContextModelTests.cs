namespace HydraForge.Infrastructure.Tests.Persistence;

using HydraForge.Infrastructure.Persistence;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using System.Linq;

public class HydraForgeDbContextModelTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql("Host=localhost;Database=hydraforge_test;Username=postgres;Password=password", o => o.UseVector())
            .Options;
    }

    [Fact]
    public void FindEntityType_MemoryEntry_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(MemoryEntry)));
    }

    [Fact]
    public void FindEntityType_DocumentChunk_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(DocumentChunk)));
    }

    [Fact]
    public void GetEntityTypes_UsersTable_Exists()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == "users");
    }

    [Fact]
    public void GetEntityTypes_CardsTable_Exists()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == "cards");
    }

    [Fact]
    public void GetEntityTypes_ProviderModelConfigsTable_Exists()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(ProviderModelConfig)));
        Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == "provider_model_configs");
    }

    [Fact]
    public void FindEntityType_ImageUsageRecord_HasRequiredProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(ImageUsageRecord));
        Assert.NotNull(entity);

        var requiredProps = new[] { "UserId", "Feature", "ProviderId", "ModelName", "ImageCount", "Resolution", "Cost" };
        foreach (var propName in requiredProps)
        {
            Assert.True(
                entity.GetProperties().Any(p => p.Name == propName),
                $"ImageUsageRecord missing property: {propName}");
        }
    }

    [Fact]
    public void FindEntityType_ImageUsageRecord_FeatureUsesSharedAiFeatureEnum()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(ImageUsageRecord));
        Assert.NotNull(entity);

        var feature = entity.FindProperty(nameof(ImageUsageRecord.Feature));
        Assert.NotNull(feature);
        Assert.Equal(typeof(AiFeature), feature.ClrType);
    }

    [Fact]
    public void FindEntityType_LlmProvider_HasRoutingAndAdapterProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(LlmProvider));
        Assert.NotNull(entity);

        var requiredProps = new[] { "ApiKeyEncrypted", "Models", "AdapterType", "ProviderType", "Tier", "FallbackProviderId" };
        foreach (var propName in requiredProps)
        {
            Assert.True(
                entity.GetProperties().Any(p => p.Name == propName),
                $"LlmProvider missing property: {propName}");
        }

        Assert.Equal(typeof(AdapterType), entity.FindProperty(nameof(LlmProvider.AdapterType))?.ClrType);
        Assert.Equal(typeof(ProviderType), entity.FindProperty(nameof(LlmProvider.ProviderType))?.ClrType);
        Assert.Equal(typeof(ModelTier), entity.FindProperty(nameof(LlmProvider.Tier))?.ClrType);
    }

    [Fact]
    public void FindEntityType_TokenUsageRecord_HasUsageBreakdownProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(TokenUsageRecord));
        Assert.NotNull(entity);

        var requiredProps = new[] { "Feature", "ProviderId", "ModelName", "InputTokens", "OutputTokens", "CachedTokens", "PipelineRunId", "Cost", "CreatedAt" };
        foreach (var propName in requiredProps)
        {
            Assert.True(
                entity.GetProperties().Any(p => p.Name == propName),
                $"TokenUsageRecord missing property: {propName}");
        }

        Assert.Equal(typeof(AiFeature), entity.FindProperty(nameof(TokenUsageRecord.Feature))?.ClrType);
    }

    [Fact]
    public void GetIndexes_UsernameNormalized_IsUnique()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var userEntity = model.FindEntityType(typeof(User));
        Assert.NotNull(userEntity);

        var usernameNormalizedIndex = userEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "UsernameNormalized"));

        Assert.NotNull(usernameNormalizedIndex);
        Assert.True(usernameNormalizedIndex.IsUnique);
    }

    [Fact]
    public void GetIndexes_CardProjectIdCardNumber_IsUnique()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var cardEntity = model.FindEntityType(typeof(Card));
        Assert.NotNull(cardEntity);

        var compositeIndex = cardEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "ProjectId") &&
                                  i.Properties.Any(p => p.Name == "CardNumber"));

        Assert.NotNull(compositeIndex);
        Assert.True(compositeIndex.IsUnique);
    }

    [Fact]
    public void GetIndexes_ProjectContextSnapshot_ProjectId_IsUnique()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(ProjectContextSnapshot));
        Assert.NotNull(entity);

        var projectIdIndex = entity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "ProjectId"));

        Assert.NotNull(projectIdIndex);
        Assert.True(projectIdIndex.IsUnique);
    }
}
