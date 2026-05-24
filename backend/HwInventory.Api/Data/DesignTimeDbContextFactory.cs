using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HwInventory.Api.Data;

/// <summary>
/// EF Core uses this at design time (<c>dotnet ef migrations add</c>) to create
/// an <see cref="AppDbContext"/> without booting the web host. It defaults to a
/// local SQLite DB so the same migration path also targets <c>DATABASE_URL</c>
/// when the env var is set to a Postgres connection string.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (!string.IsNullOrWhiteSpace(url) && (url.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || url.Contains("postgres", StringComparison.OrdinalIgnoreCase)))
        {
            optionsBuilder.UseNpgsql(url).UseSnakeCaseNamingConvention();
        }
        else
        {
            optionsBuilder.UseSqlite("Data Source=hw_inventory.db").UseSnakeCaseNamingConvention();
        }

        return new AppDbContext(optionsBuilder.Options);
    }
}
