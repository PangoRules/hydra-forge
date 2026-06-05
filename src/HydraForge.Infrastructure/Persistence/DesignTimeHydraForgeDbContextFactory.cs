namespace HydraForge.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

public class DesignTimeHydraForgeDbContextFactory : IDesignTimeDbContextFactory<HydraForgeDbContext>
{
    public HydraForgeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HydraForgeDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=hydraforge;Username=postgres;Password=password", o => o.UseVector());

        return new HydraForgeDbContext(optionsBuilder.Options);
    }
}