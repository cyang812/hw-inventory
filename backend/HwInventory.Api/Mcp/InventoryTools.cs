using System.ComponentModel;
using System.Text.Json;
using HwInventory.Api.Config;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using HwInventory.Api.Services;
using ModelContextProtocol.Server;

namespace HwInventory.Api.Mcp;

/// <summary>
/// MCP tool surface for the personal hardware inventory. Reads are broad; writes
/// are limited to safe, intent-shaped operations (no archive, no taxonomy mutation,
/// no bulk import/export, no unlink). Agents that need those go through REST.
/// </summary>
[McpServerToolType]
public class InventoryTools
{
    private readonly HardwareService _hardware;
    private readonly ProjectService _projects;
    private readonly ActivityService _activities;
    private readonly HardwareConfigService _configs;
    private readonly LoanService _loans;
    private readonly DashboardService _dashboard;
    private readonly HwInventoryOptions _opts;

    public InventoryTools(
        HardwareService hardware, ProjectService projects, ActivityService activities,
        HardwareConfigService configs, LoanService loans, DashboardService dashboard,
        HwInventoryOptions opts)
    {
        _hardware = hardware; _projects = projects; _activities = activities;
        _configs = configs; _loans = loans; _dashboard = dashboard; _opts = opts;
    }

    // ---------- Reads ----------

    [McpServerTool(Name = "list_hardware"), Description("List hardware with the same filters as the REST /api/hardware endpoint.")]
    public async Task<ListResponse<HardwareSummaryDto>> ListHardware(
        [Description("Substring search across name, model, manufacturer, notes, serial_number.")] string? q = null,
        [Description("Repeatable category id; multiple values are AND.")] int[]? categoryId = null,
        [Description("Repeatable tag id; multiple values are AND.")] int[]? tagId = null,
        [Description("Filter by status: available|inUse|loaned|archived|sold|lost.")] string? status = null,
        [Description("Filter by condition: working|partial|broken|unknown.")] string? condition = null,
        [Description("Only show items idle (no `used`-style activity) for this many days.")] int? idleDays = null,
        [Description("Include archived items (soft-deleted).")] bool includeArchived = false,
        int limit = 50,
        int offset = 0,
        string? sort = null,
        CancellationToken ct = default)
    {
        var filter = new HardwareListFilter(
            limit, offset, sort, q,
            CategoryIds: categoryId, TagIds: tagId,
            Status: ParseEnum<HardwareStatus>(status),
            Condition: ParseEnum<HardwareCondition>(condition),
            IdleDays: idleDays,
            IncludeArchived: includeArchived);
        return await _hardware.ListAsync(filter, ct);
    }

    [McpServerTool(Name = "get_hardware"), Description("Full hardware detail including categories, tags, recent activities, current configs, and active loan.")]
    public Task<HardwareDto> GetHardware([Description("Hardware id.")] int id, CancellationToken ct = default) =>
        _hardware.GetAsync(id, includeArchived: true, ct);

    [McpServerTool(Name = "list_idle_hardware"), Description("Idle hardware (no recent `used`-style activity) — wraps /api/dashboard/idle.")]
    public Task<ListResponse<HardwareSummaryDto>> ListIdleHardware(
        [Description("Idle threshold in days (default: configured IDLE_DEFAULT_DAYS).")] int? days = null,
        int limit = 50, int offset = 0, CancellationToken ct = default) =>
        _dashboard.ListIdleAsync(days ?? _opts.IdleDefaultDays, limit, offset, ct);

    [McpServerTool(Name = "list_suggestions"), Description("Idle hardware that has no active project — wraps /api/dashboard/suggestions.")]
    public Task<ListResponse<HardwareSummaryDto>> ListSuggestions(
        int? days = null, int limit = 50, int offset = 0, CancellationToken ct = default) =>
        _dashboard.ListSuggestionsAsync(days ?? _opts.IdleDefaultDays, limit, offset, ct);

    [McpServerTool(Name = "list_overdue_loans"), Description("Open loans whose due date is in the past.")]
    public Task<ListResponse<LoanDto>> ListOverdueLoans(CancellationToken ct = default) =>
        _dashboard.ListOverdueLoansAsync(ct);

    [McpServerTool(Name = "get_dashboard_stats"), Description("Counts by status, condition, category, project status, plus 30-day activity volume.")]
    public Task<DashboardStatsDto> GetDashboardStats(CancellationToken ct = default) =>
        _dashboard.StatsAsync(ct);

    [McpServerTool(Name = "list_projects"), Description("List projects with the same filters as /api/projects.")]
    public Task<ListResponse<ProjectSummaryDto>> ListProjects(
        string? q = null, string? status = null, string? priority = null,
        bool includeArchived = false, int limit = 50, int offset = 0, string? sort = null,
        CancellationToken ct = default) =>
        _projects.ListAsync(new ProjectListFilter(
            limit, offset, sort, q,
            Status: ParseEnum<ProjectStatus>(status),
            Priority: ParseEnum<ProjectPriority>(priority),
            IncludeArchived: includeArchived), ct);

    [McpServerTool(Name = "get_project"), Description("Full project detail including linked hardware.")]
    public Task<ProjectDto> GetProject([Description("Project id.")] int id, CancellationToken ct = default) =>
        _projects.GetAsync(id, includeArchived: true, ct);

    [McpServerTool(Name = "list_activities"), Description("Cross-cutting activity timeline.")]
    public Task<ListResponse<ActivityDto>> ListActivities(
        int? hardwareId = null, int? projectId = null, string? kind = null,
        DateTimeOffset? occurredAfter = null, DateTimeOffset? occurredBefore = null,
        int limit = 50, int offset = 0, string? sort = "-occurredAt", CancellationToken ct = default) =>
        _activities.ListAsync(new ActivityListFilter(
            limit, offset, sort,
            HardwareId: hardwareId, ProjectId: projectId,
            Kind: ParseEnum<ActivityKind>(kind),
            OccurredAfter: occurredAfter, OccurredBefore: occurredBefore), ct);

    // ---------- Writes (safe, intent-shaped) ----------

    [McpServerTool(Name = "mark_used_today"), Description("Log a `used` activity for the given hardware at the current time.")]
    public Task<ActivityDto> MarkUsedToday([Description("Hardware id.")] int hardwareId, CancellationToken ct = default) =>
        _hardware.MarkUsedTodayAsync(hardwareId, ct);

    [McpServerTool(Name = "log_activity"), Description("Generic activity create: any kind, optional description / metadata / occurred_at / project_id.")]
    public Task<ActivityDto> LogActivity(
        [Description("Hardware id.")] int hardwareId,
        [Description("Activity kind: used|flashed|repaired|measured|configured|inspected|moved|note.")] string kind,
        [Description("Free-form description (markdown allowed).")] string? description = null,
        [Description("ISO-8601 timestamp. Defaults to now.")] DateTimeOffset? occurredAt = null,
        [Description("Optional Project to associate.")] int? projectId = null,
        [Description("Free-form JSON metadata as a string (parsed by the server).")] string? metadataJson = null,
        CancellationToken ct = default)
    {
        var parsedKind = ParseEnum<ActivityKind>(kind) ?? throw new ValidationException($"unknown activity kind '{kind}'");
        JsonElement? meta = string.IsNullOrWhiteSpace(metadataJson) ? null :
            JsonDocument.Parse(metadataJson).RootElement;
        return _activities.CreateAsync(hardwareId,
            new ActivityCreateDto(parsedKind, description, meta, occurredAt, projectId), ct);
    }

    [McpServerTool(Name = "link_project"), Description("Idempotent link of a hardware item to a project, with optional role.")]
    public Task<HardwareProjectLinkDto> LinkProject(
        [Description("Hardware id.")] int hardwareId,
        [Description("Project id.")] int projectId,
        [Description("Optional role: e.g. 'host', 'target', 'sacrificial'.")] string? role = null,
        CancellationToken ct = default) =>
        _hardware.LinkProjectAsync(hardwareId, projectId, role, ct);

    [McpServerTool(Name = "record_firmware"), Description("Record a new firmware/OS/bootloader/config install for a hardware item.")]
    public Task<HardwareConfigDto> RecordFirmware(
        [Description("Hardware id.")] int hardwareId,
        [Description("Config kind: firmware|os|bootloader|config.")] string kind,
        [Description("Name (e.g. 'Raspberry Pi OS Lite').")] string name,
        [Description("Version string (e.g. '2024-07-04').")] string? version = null,
        [Description("Optional free-form notes.")] string? notes = null,
        [Description("Installation timestamp. Defaults to now.")] DateTimeOffset? installedAt = null,
        [Description("Mark this as the current install (clears the previous current entry of same kind).")] bool isCurrent = true,
        CancellationToken ct = default)
    {
        var parsedKind = ParseEnum<HardwareConfigKind>(kind) ?? throw new ValidationException($"unknown kind '{kind}'");
        return _configs.RecordAsync(hardwareId,
            new HardwareConfigCreateDto(parsedKind, name, version, notes, installedAt, isCurrent, LogActivity: true), ct);
    }

    [McpServerTool(Name = "start_loan"), Description("Open a loan for a hardware item.")]
    public Task<LoanDto> StartLoan(
        [Description("Hardware id.")] int hardwareId,
        [Description("Who has it now.")] string loanedTo,
        [Description("Due date (ISO-8601).")] DateTimeOffset? dueAt = null,
        [Description("Optional notes.")] string? notes = null,
        CancellationToken ct = default) =>
        _loans.StartAsync(hardwareId, new LoanCreateDto(loanedTo, null, dueAt, notes), ct);

    [McpServerTool(Name = "return_loan"), Description("Close an open loan.")]
    public Task<LoanDto> ReturnLoan(
        [Description("Hardware id.")] int hardwareId,
        [Description("Loan id (from list_loans / get_hardware).")] int loanId,
        [Description("Return timestamp. Defaults to now.")] DateTimeOffset? returnedAt = null,
        CancellationToken ct = default) =>
        _loans.ReturnAsync(hardwareId, loanId, returnedAt, ct);

    [McpServerTool(Name = "create_project"), Description("Create a Project.")]
    public Task<ProjectDto> CreateProject(
        [Description("Title.")] string title,
        [Description("Optional URL slug; derived from title if omitted.")] string? slug = null,
        [Description("Optional markdown description.")] string? description = null,
        [Description("Status: idea|planned|inProgress|paused|done|abandoned.")] string status = "idea",
        [Description("Priority: low|medium|high.")] string priority = "medium",
        CancellationToken ct = default) =>
        _projects.CreateAsync(new ProjectCreateDto(
            title, slug, description,
            ParseEnum<ProjectStatus>(status) ?? ProjectStatus.Idea,
            ParseEnum<ProjectPriority>(priority) ?? ProjectPriority.Medium), ct);

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct =>
        string.IsNullOrWhiteSpace(value) ? null :
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var e) ? e : null;
}

/// <summary>
/// MCP resources — URI-addressable views the agent can pull into context.
/// </summary>
[McpServerResourceType]
public class InventoryResources(HardwareService hardware, ProjectService projects, DashboardService dashboard)
{
    [McpServerResource(UriTemplate = "hwinv://hardware/{id}", Name = "Hardware item", MimeType = "application/json")]
    [Description("Full hardware detail in JSON, equivalent to GET /api/hardware/{id}.")]
    public async Task<string> Hardware(int id, CancellationToken ct = default)
    {
        var dto = await hardware.GetAsync(id, includeArchived: true, ct);
        return JsonSerializer.Serialize(dto, JsonOpts);
    }

    [McpServerResource(UriTemplate = "hwinv://project/{id}", Name = "Project", MimeType = "application/json")]
    [Description("Full project detail in JSON, equivalent to GET /api/projects/{id}.")]
    public async Task<string> Project(int id, CancellationToken ct = default)
    {
        var dto = await projects.GetAsync(id, includeArchived: true, ct);
        return JsonSerializer.Serialize(dto, JsonOpts);
    }

    [McpServerResource(UriTemplate = "hwinv://dashboard", Name = "Dashboard snapshot", MimeType = "application/json")]
    [Description("Current dashboard counts (idle, overdue, stats).")]
    public async Task<string> Dashboard(CancellationToken ct = default)
    {
        var stats = await dashboard.StatsAsync(ct);
        return JsonSerializer.Serialize(stats, JsonOpts);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
