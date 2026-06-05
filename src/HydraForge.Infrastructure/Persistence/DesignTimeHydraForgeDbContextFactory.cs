namespace HydraForge.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

public class DesignTimeHydraForgeDbContextFactory : IDesignTimeDbContextFactory<HydraForgeDbContext>
{
    public HydraForgeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HydraForgeDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=hydraforge;Username=hydraforge;Password=hydr4l0c4", o => o.UseVector());

        return new HydraForgeDbContext(optionsBuilder.Options);
    }
}