using System.Text.Json;
using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using HwInventory.Api.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projects").WithTags("Projects");

        g.MapGet("", async (HttpRequest req, ProjectService svc, CancellationToken ct) =>
        {
            var (limit, offset, sort, q) = QueryHelpers.ParseList(req);
            var filter = new ProjectListFilter(
                limit, offset, sort, q,
                Status: QueryHelpers.ParseEnum<ProjectStatus>(req, "status"),
                Priority: QueryHelpers.ParseEnum<ProjectPriority>(req, "priority"),
                IncludeArchived: QueryHelpers.ParseBool(req, "include_archived", false),
                CreatedAfter: QueryHelpers.ParseDate(req, "created_after"),
                CreatedBefore: QueryHelpers.ParseDate(req, "created_before"));
            return Results.Ok(await svc.ListAsync(filter, ct));
        });

        g.MapGet("{id:int}", async (int id, HttpRequest req, ProjectService svc, CancellationToken ct) =>
        {
            var includeArchived = QueryHelpers.ParseBool(req, "include_archived", true);
            return Results.Ok(await svc.GetAsync(id, includeArchived, ct));
        });

        g.MapPost("", async ([FromBody] ProjectCreateDto dto, ProjectService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateAsync(dto, ct);
            return Results.Created($"/api/projects/{created.Id}", created);
        });

        g.MapPatch("{id:int}", async (int id, HttpRequest req, AppDbContext db, ProjectService svc, CancellationToken ct) =>
        {
            var doc = await HardwareEndpoints.ReadPatchAsync(req, ct);
            HardwareEndpoints.ValidatePatchAllowList(doc, ProjectService.PatchAllowList);
            var p = await db.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"project {id}");
            await PatchHelpers.ApplyAsync<Project, ProjectSummaryDto>(doc, p,
                getter: _ => null!, fieldWriters: ProjectPatchWriters.Map, ct: ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await svc.GetAsync(id, includeArchived: true, ct));
        });

        g.MapDelete("{id:int}", async (int id, HttpRequest req, ProjectService svc, CancellationToken ct) =>
        {
            var hard = QueryHelpers.ParseBool(req, "hard", false);
            if (hard) { await svc.HardDeleteAsync(id, ct); return Results.NoContent(); }
            await svc.ArchiveAsync(id, ct);
            return Results.NoContent();
        });

        g.MapPost("{id:int}/unarchive", async (int id, ProjectService svc, CancellationToken ct) =>
            Results.Ok(await svc.UnarchiveAsync(id, ct)));

        g.MapGet("{id:int}/hardware", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.HardwareProjects.AsNoTracking()
                .Where(hp => hp.ProjectId == id)
                .Select(hp => new ProjectHardwareLinkDto(hp.HardwareId, hp.Hardware.Name, hp.Role, hp.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(new ListResponse<ProjectHardwareLinkDto>(rows, rows.Count, rows.Count, 0));
        });

        g.MapPut("{id:int}/hardware/{hid:int}", async (int id, int hid, [FromBody] LinkBody? body, HardwareService svc, CancellationToken ct) =>
        {
            var link = await svc.LinkProjectAsync(hid, id, body?.Role, ct);
            return Results.Ok(link);
        });
        g.MapDelete("{id:int}/hardware/{hid:int}", async (int id, int hid, HardwareService svc, CancellationToken ct) =>
        {
            await svc.UnlinkProjectAsync(hid, id, ct);
            return Results.NoContent();
        });

        return app;
    }
}

public static class ProjectPatchWriters
{
    private static string? StrOrNull(JsonElement? e) => e is null || e.Value.ValueKind == JsonValueKind.Null ? null : e.Value.GetString();
    private static DateTimeOffset? DateOrNull(JsonElement? e) =>
        e is null || e.Value.ValueKind == JsonValueKind.Null ? null :
        DateTimeOffset.TryParse(e.Value.GetString(), out var d) ? d : null;
    private static JsonDocument? DocOrNull(JsonElement? e) =>
        e is null || e.Value.ValueKind == JsonValueKind.Null ? null : JsonDocument.Parse(e.Value.GetRawText());
    private static TEnum EnumOrThrow<TEnum>(JsonElement? e, string field) where TEnum : struct
    {
        if (e is null || e.Value.ValueKind != JsonValueKind.String)
            throw new PreconditionFailedException($"{field} must be a string enum value");
        var s = e.Value.GetString() ?? "";
        return Enum.TryParse<TEnum>(s, true, out var v) ? v : throw new PreconditionFailedException($"invalid {field}: {s}");
    }

    public static readonly Dictionary<string, Action<Project, JsonElement?>> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/title"] = (p, e) => { var s = StrOrNull(e); if (string.IsNullOrWhiteSpace(s)) throw new ValidationException("title cannot be empty"); p.Title = s; },
        ["/slug"] = (p, e) => { var s = StrOrNull(e); if (string.IsNullOrWhiteSpace(s)) throw new ValidationException("slug cannot be empty"); p.Slug = s; },
        ["/description"] = (p, e) => p.Description = StrOrNull(e),
        ["/status"] = (p, e) =>
        {
            p.Status = EnumOrThrow<ProjectStatus>(e, "status");
            if (p.Status == ProjectStatus.Done && p.CompletedAt is null) p.CompletedAt = DateTimeOffset.UtcNow;
        },
        ["/priority"] = (p, e) => p.Priority = EnumOrThrow<ProjectPriority>(e, "priority"),
        ["/startedAt"] = (p, e) => p.StartedAt = DateOrNull(e),
        ["/targetDate"] = (p, e) => p.TargetDate = DateOrNull(e),
        ["/completedAt"] = (p, e) => p.CompletedAt = DateOrNull(e),
        ["/notes"] = (p, e) => p.Notes = StrOrNull(e),
        ["/links"] = (p, e) => p.Links = DocOrNull(e),
    };
}

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/activities", async (HttpRequest req, ActivityService svc, CancellationToken ct) =>
        {
            var (limit, offset, sort, _) = QueryHelpers.ParseList(req);
            var filter = new ActivityListFilter(
                limit, offset, sort,
                HardwareId: req.Query.ContainsKey("hardware_id") ? QueryHelpers.ParseInt(req, "hardware_id", 0) : null,
                ProjectId: req.Query.ContainsKey("project_id") ? QueryHelpers.ParseInt(req, "project_id", 0) : null,
                Kind: QueryHelpers.ParseEnum<ActivityKind>(req, "kind"),
                OccurredAfter: QueryHelpers.ParseDate(req, "occurred_after"),
                OccurredBefore: QueryHelpers.ParseDate(req, "occurred_before"));
            return Results.Ok(await svc.ListAsync(filter, ct));
        }).WithTags("Activities");

        return app;
    }
}

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        g.MapGet("stats", async (DashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.StatsAsync(ct)));

        g.MapGet("idle", async (HttpRequest req, DashboardService svc, Config.HwInventoryOptions opts, CancellationToken ct) =>
        {
            var days = QueryHelpers.ParseInt(req, "days", opts.IdleDefaultDays);
            var limit = QueryHelpers.ParseInt(req, "limit", 50);
            var offset = QueryHelpers.ParseInt(req, "offset", 0);
            return Results.Ok(await svc.ListIdleAsync(days, limit, offset, ct));
        });

        g.MapGet("suggestions", async (HttpRequest req, DashboardService svc, Config.HwInventoryOptions opts, CancellationToken ct) =>
        {
            var days = QueryHelpers.ParseInt(req, "days", opts.IdleDefaultDays);
            var limit = QueryHelpers.ParseInt(req, "limit", 50);
            var offset = QueryHelpers.ParseInt(req, "offset", 0);
            return Results.Ok(await svc.ListSuggestionsAsync(days, limit, offset, ct));
        });

        g.MapGet("overdue-loans", async (DashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListOverdueLoansAsync(ct)));

        return app;
    }
}
