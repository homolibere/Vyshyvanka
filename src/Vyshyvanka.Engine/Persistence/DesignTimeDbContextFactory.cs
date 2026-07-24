using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vyshyvanka.Engine.Persistence;

/// <summary>
/// Design-time factory for generating EF Core migrations targeting PostgreSQL.
/// Used by <c>dotnet ef migrations add</c> — not invoked at runtime.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VyshyvankaDbContext>
{
    public VyshyvankaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VyshyvankaDbContext>();

        // Use a dummy PostgreSQL connection string for migration generation.
        // This never connects — EF Core only needs the provider to produce correct SQL types.
        optionsBuilder.UseNpgsql("Host=localhost;Database=vyshyvanka_design;Username=postgres;Password=postgres");

        return new VyshyvankaDbContext(optionsBuilder.Options);
    }
}
