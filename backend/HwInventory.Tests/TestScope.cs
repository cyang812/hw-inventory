using HwInventory.Api.Config;
using HwInventory.Api.Data;
using HwInventory.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HwInventory.Tests;

/// <summary>
/// Builds an in-memory SQLite-backed DI scope so unit tests run against the same
/// EF Core stack the app uses, including converters, naming conventions, and the
/// soft-delete query filter.
/// </summary>
public static class TestScope
{
    public static (ServiceProvider Sp, AppDbContext Db) Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new HwInventoryOptions { DatabaseUrl = "Data Source=:memory:", IdleDefaultDays = 90 });
        services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=:memory:").UseSnakeCaseNamingConvention());
        services.AddScoped<ActivityService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<TagService>();
        services.AddScoped<HardwareService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<HardwareConfigService>();
        services.AddScoped<LoanService>();
        services.AddScoped<DashboardService>();

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        // In-memory SQLite needs an open connection to keep the schema; EnsureCreated builds it.
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return (sp, db);
    }
}
