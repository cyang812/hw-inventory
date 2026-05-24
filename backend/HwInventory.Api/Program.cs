using HwInventory.Api.Config;
using HwInventory.Api.Data;
using HwInventory.Api.Endpoints;
using HwInventory.Api.Seed;
using HwInventory.Api.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ----- Configuration -----
var opts = HwInventoryOptionsLoader.Load(builder.Configuration);
builder.Services.AddSingleton(opts);

// Production refuses to start without an auth token or with the default loopback-only bind
// (operator must consciously opt in to externally reachable surface).
if (opts.IsProduction)
{
    if (string.IsNullOrWhiteSpace(opts.AuthToken))
    {
        Console.Error.WriteLine("FATAL: APP_ENV=production requires AUTH_TOKEN to be set.");
        Environment.Exit(2);
    }
    if (string.IsNullOrWhiteSpace(opts.BindAddress) || opts.BindAddress.Contains("127.0.0.1"))
    {
        Console.Error.WriteLine("FATAL: APP_ENV=production requires BIND_ADDRESS to be an explicit non-loopback URL.");
        Environment.Exit(2);
    }
}

// ----- EF Core -----
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (opts.UsePostgres)
        o.UseNpgsql(opts.DatabaseUrl);
    else
        o.UseSqlite(opts.DatabaseUrl);
    o.UseSnakeCaseNamingConvention();
    // We intentionally don't propagate the Hardware soft-delete filter to dependent
    // entities (Activities, Configs, Loans, HardwareProjects). Read paths that fetch
    // dependents include the parent (or use IgnoreQueryFilters) so the warning is noise.
    o.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
});

// ----- Domain services -----
builder.Services.AddScoped<ActivityService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<HardwareService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<HardwareConfigService>();
builder.Services.AddScoped<LoanService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ImportExportService>();

// ----- ASP.NET Core, OpenAPI, gRPC, MCP, CORS -----
builder.Services.AddOpenApi("v1");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(
        System.Text.Json.JsonNamingPolicy.CamelCase));
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("*")));

builder.WebHost.UseUrls(opts.BindAddress);

var app = builder.Build();

// ----- Pipeline -----
app.UseCors();

// SPA assets, when present (baked into the published image's wwwroot/). Sits BEFORE the
// bearer-auth middleware so the HTML/JS load without a token; API calls are still guarded.
var spaRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var hasSpa = File.Exists(Path.Combine(spaRoot, "index.html"));
if (hasSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseMiddleware<BearerAuthMiddleware>();
app.UseMiddleware<DomainExceptionMiddleware>();

// Open the OpenAPI document + Scalar UI; both are unauthenticated only when AUTH_TOKEN is unset.
app.MapOpenApi();
app.MapScalarApiReference(o =>
{
    o.WithTitle("hw-inventory API")
     .WithTheme(ScalarTheme.Mars);
});

// ----- Endpoints -----
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.MapCategoryEndpoints();
app.MapTagEndpoints();
app.MapHardwareEndpoints();
app.MapProjectEndpoints();
app.MapActivityEndpoints();
app.MapDashboardEndpoints();
app.MapImportExportEndpoints();

// gRPC services
app.MapGrpcService<HwInventory.Api.Grpc.HealthGrpcService>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

// MCP
app.MapMcp("/mcp");

// SPA fallback for client-side router (Vue Router uses history mode).
// Lives after all real endpoints so /api, /mcp, /scalar etc. take precedence.
if (hasSpa) app.MapFallbackToFile("index.html");

// ----- Boot tasks -----
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!opts.UsePostgres)
    {
        // For SQLite dev, make sure the schema is up-to-date so users can just run the app.
        await db.Database.MigrateAsync();
    }

    if (args.Any(a => string.Equals(a, "--seed", StringComparison.OrdinalIgnoreCase)))
    {
        await DemoSeeder.SeedAsync(scope.ServiceProvider);
        Console.WriteLine("Seed complete.");
        return;
    }
}

app.Run();

public partial class Program;
