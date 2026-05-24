namespace HwInventory.Api.Config;

/// <summary>
/// Strongly-typed binding for the <c>HwInventory</c> section in appsettings.json.
/// Environment variables (<c>DATABASE_URL</c>, <c>APP_ENV</c>, <c>AUTH_TOKEN</c>,
/// <c>BIND_ADDRESS</c>, <c>IDLE_DEFAULT_DAYS</c>, <c>ATTACHMENTS_DIR</c>) override
/// these defaults in <see cref="HwInventoryOptionsLoader.Load"/>.
/// </summary>
public class HwInventoryOptions
{
    public const string SectionName = "HwInventory";

    public string DatabaseUrl { get; set; } = "Data Source=hw_inventory.db";
    public string BindAddress { get; set; } = "http://127.0.0.1:5080";
    public string GrpcBindAddress { get; set; } = "";
    public string AppEnv { get; set; } = "development";
    public string AuthToken { get; set; } = "";
    public int IdleDefaultDays { get; set; } = 90;
    public string AttachmentsDir { get; set; } = "attachments";

    public bool IsProduction => string.Equals(AppEnv, "production", StringComparison.OrdinalIgnoreCase);
    public bool UsePostgres =>
        !string.IsNullOrWhiteSpace(DatabaseUrl) &&
        (DatabaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
         DatabaseUrl.Contains("postgres", StringComparison.OrdinalIgnoreCase));
}

public static class HwInventoryOptionsLoader
{
    public static HwInventoryOptions Load(IConfiguration cfg)
    {
        var opts = new HwInventoryOptions();
        cfg.GetSection(HwInventoryOptions.SectionName).Bind(opts);

        // Env-var overrides — keep names matching plan §2 / README.
        var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(dbUrl)) opts.DatabaseUrl = dbUrl;

        var bind = Environment.GetEnvironmentVariable("BIND_ADDRESS");
        if (!string.IsNullOrWhiteSpace(bind)) opts.BindAddress = bind;

        var grpcBind = Environment.GetEnvironmentVariable("GRPC_BIND_ADDRESS");
        if (!string.IsNullOrWhiteSpace(grpcBind)) opts.GrpcBindAddress = grpcBind;

        var appEnv = Environment.GetEnvironmentVariable("APP_ENV");
        if (!string.IsNullOrWhiteSpace(appEnv)) opts.AppEnv = appEnv;

        var token = Environment.GetEnvironmentVariable("AUTH_TOKEN");
        if (!string.IsNullOrWhiteSpace(token)) opts.AuthToken = token;

        var idleDays = Environment.GetEnvironmentVariable("IDLE_DEFAULT_DAYS");
        if (int.TryParse(idleDays, out var d) && d > 0) opts.IdleDefaultDays = d;

        var attachments = Environment.GetEnvironmentVariable("ATTACHMENTS_DIR");
        if (!string.IsNullOrWhiteSpace(attachments)) opts.AttachmentsDir = attachments;

        return opts;
    }
}
