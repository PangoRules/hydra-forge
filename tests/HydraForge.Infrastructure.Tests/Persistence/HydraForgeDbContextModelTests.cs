namespace HydraForge.Infrastructure.Tests.Persistence;

using HydraForge.Infrastructure.Persistence;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Entities.PersonalSpace;
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
