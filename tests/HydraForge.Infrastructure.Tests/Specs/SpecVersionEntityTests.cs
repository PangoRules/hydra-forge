namespace HydraForge.Infrastructure.Tests.Specs;

using HydraForge.Infrastructure.Persistence;
using HydraForge.Domain.Entities.ProjectSpace;
using Microsoft.EntityFrameworkCore;

public class SpecVersionEntityTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql("Host=localhost;Database=hydraforge_test;Username=postgres;Password=password", o => o.UseVector())
            .Options;
    }

    [Fact]
    public void FindEntityType_SpecVersion_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        var entity = model.FindEntityType(typeof(SpecVersion));
        Assert.NotNull(entity);
    }

    [Fact]
    public void VersionToSpec_Relationship_IsConfigured()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        var versionEntity = model.FindEntityType(typeof(SpecVersion));
        Assert.NotNull(versionEntity);
        var specEntity = model.FindEntityType(typeof(Spec));
        Assert.NotNull(specEntity);

        var fk = versionEntity.GetForeignKeys().FirstOrDefault(k => k.PrincipalEntityType == specEntity);
        Assert.NotNull(fk);
        Assert.Equal("SpecId", fk.Properties.First().Name);
    }

    [Fact]
    public void SpecIdVersion_UniqueIndex()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        var versionEntity = model.FindEntityType(typeof(SpecVersion));
        Assert.NotNull(versionEntity);

        var specIdVersionIndex = versionEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "SpecId") &&
                                  i.Properties.Any(p => p.Name == "Version"));
        Assert.NotNull(specIdVersionIndex);
        Assert.True(specIdVersionIndex.IsUnique);
    }
}
