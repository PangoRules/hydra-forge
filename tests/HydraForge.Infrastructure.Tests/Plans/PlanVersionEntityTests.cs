namespace HydraForge.Infrastructure.Tests.Plans;

using HydraForge.Infrastructure.Persistence;
using HydraForge.Domain.Entities.ProjectSpace;
using Microsoft.EntityFrameworkCore;

public class PlanVersionEntityTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql("Host=localhost;Database=hydraforge_test;Username=postgres;Password=password", o => o.UseVector())
            .Options;
    }

    [Fact]
    public void FindEntityType_PlanVersion_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        var entity = model.FindEntityType(typeof(PlanVersion));
        Assert.NotNull(entity);
    }

    [Fact]
    public void VersionToPlan_Relationship_IsConfigured()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        var versionEntity = model.FindEntityType(typeof(PlanVersion));
        Assert.NotNull(versionEntity);
        var planEntity = model.FindEntityType(typeof(Plan));
        Assert.NotNull(planEntity);

        var fk = versionEntity.GetForeignKeys().FirstOrDefault(k => k.PrincipalEntityType == planEntity);
        Assert.NotNull(fk);
        Assert.Equal("PlanId", fk.Properties.First().Name);
    }

    [Fact]
    public void PlanIdVersion_UniqueIndex()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        var versionEntity = model.FindEntityType(typeof(PlanVersion));
        Assert.NotNull(versionEntity);

        var planIdVersionIndex = versionEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "PlanId") &&
                                  i.Properties.Any(p => p.Name == "Version"));
        Assert.NotNull(planIdVersionIndex);
        Assert.True(planIdVersionIndex.IsUnique);
    }
}
