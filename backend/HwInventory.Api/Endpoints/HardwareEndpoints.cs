using System.Text.Json;
using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using HwInventory.Api.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Endpoints;

public static class HardwareEndpoints
{
    public static IEndpointRouteBuilder MapHardwareEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/hardware").WithTags("Hardware");

        g.MapGet("", async (HttpRequest req, HardwareService svc, CancellationToken ct) =>
        {
            var (limit, offset, sort, q) = QueryHelpers.ParseList(req);
            var filter = new HardwareListFilter(
                limit, offset, sort, q,
                CategoryIds: QueryHelpers.ParseIntList(req, "category"),
                TagIds: QueryHelpers.ParseIntList(req, "tag"),
                Status: QueryHelpers.ParseEnum<HardwareStatus>(req, "status"),
                Condition: QueryHelpers.ParseEnum<HardwareCondition>(req, "condition"),
                IdleDays: req.Query.ContainsKey("idle_days") ? QueryHelpers.ParseInt(req, "idle_days", 0) : null,
                IncludeArchived: QueryHelpers.ParseBool(req, "include_archived", false),
                CreatedAfter: QueryHelpers.ParseDate(req, "created_after"),
                CreatedBefore: QueryHelpers.ParseDate(req, "created_before"),
                UpdatedAfter: QueryHelpers.ParseDate(req, "updated_after"),
                UpdatedBefore: QueryHelpers.ParseDate(req, "updated_before"));
            return Results.Ok(await svc.ListAsync(filter, ct));
        });

        g.MapGet("{id:int}", async (int id, HttpRequest req, HardwareService svc, CancellationToken ct) =>
        {
            var includeArchived = QueryHelpers.ParseBool(req, "include_archived", true);
            return Results.Ok(await svc.GetAsync(id, includeArchived, ct));
        });

        g.MapPost("", async ([FromBody] HardwareCreateDto dto, HardwareService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateAsync(dto, ct);
            return Results.Created($"/api/hardware/{created.Id}", created);
        });

        g.MapPatch("{id:int}", async (int id, HttpRequest req, AppDbContext db, HardwareService svc, CancellationToken ct) =>
        {
            var doc = await ReadPatchAsync(req, ct);
            ValidatePatchAllowList(doc, HardwareService.PatchAllowList);
            var hw = await db.Hardware.IgnoreQueryFilters()
                .FirstOrDefaultAsync(h => h.Id == id, ct)
                ?? throw new NotFoundException($"hardware {id}");
            // Apply against a wire-shape (camelCase JSON) view to keep paths consistent with the public API.
            await PatchHelpers.ApplyAsync(doc, hw,
                getter: h => Mapping.ToSummary(h),
                fieldWriters: HardwarePatchWriters.Map,
                ct: ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await svc.GetAsync(id, includeArchived: true, ct));
        });

        g.MapDelete("{id:int}", async (int id, HttpRequest req, HardwareService svc, CancellationToken ct) =>
        {
            var hard = QueryHelpers.ParseBool(req, "hard", false);
            if (hard)
            {
                await svc.HardDeleteAsync(id, ct);
                return Results.NoContent();
            }
            await svc.ArchiveAsync(id, ct);
            return Results.NoContent();
        });

        g.MapPost("{id:int}/unarchive", async (int id, HardwareService svc, CancellationToken ct) =>
            Results.Ok(await svc.UnarchiveAsync(id, ct)));

        // Activities subresource
        g.MapGet("{id:int}/activities", async (int id, HttpRequest req, ActivityService svc, CancellationToken ct) =>
        {
            var (limit, offset, sort, _) = QueryHelpers.ParseList(req);
            var filter = new ActivityListFilter(
                limit, offset, sort,
                HardwareId: id,
                ProjectId: req.Query.ContainsKey("project_id") ? QueryHelpers.ParseInt(req, "project_id", 0) : null,
                Kind: QueryHelpers.ParseEnum<ActivityKind>(req, "kind"),
                OccurredAfter: QueryHelpers.ParseDate(req, "occurred_after"),
                OccurredBefore: QueryHelpers.ParseDate(req, "occurred_before"));
            return Results.Ok(await svc.ListAsync(filter, ct));
        });

        g.MapPost("{id:int}/activities", async (int id, [FromBody] ActivityCreateDto dto, ActivityService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateAsync(id, dto, ct);
            return Results.Created($"/api/hardware/{id}/activities/{created.Id}", created);
        });

        // Quick action
        g.MapPost("{id:int}/mark-used-today", async (int id, HardwareService svc, CancellationToken ct) =>
            Results.Ok(await svc.MarkUsedTodayAsync(id, ct)));

        // Category linkage
        g.MapPut("{id:int}/categories/{cid:int}", async (int id, int cid, HardwareService svc, CancellationToken ct) =>
        {
            await svc.LinkCategoryAsync(id, cid, ct);
            return Results.Ok();
        });
        g.MapDelete("{id:int}/categories/{cid:int}", async (int id, int cid, HardwareService svc, CancellationToken ct) =>
        {
            await svc.UnlinkCategoryAsync(id, cid, ct);
            return Results.NoContent();
        });

        // Tag linkage
        g.MapPut("{id:int}/tags/{tid:int}", async (int id, int tid, HardwareService svc, CancellationToken ct) =>
        {
            await svc.LinkTagAsync(id, tid, ct);
            return Results.Ok();
        });
        g.MapDelete("{id:int}/tags/{tid:int}", async (int id, int tid, HardwareService svc, CancellationToken ct) =>
        {
            await svc.UnlinkTagAsync(id, tid, ct);
            return Results.NoContent();
        });

        // Project linkage
        g.MapGet("{id:int}/projects", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.HardwareProjects.AsNoTracking()
                .Where(hp => hp.HardwareId == id)
                .Select(hp => new HardwareProjectLinkDto(hp.ProjectId, hp.Project.Title, hp.Role, hp.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(new ListResponse<HardwareProjectLinkDto>(rows, rows.Count, rows.Count, 0));
        });

        g.MapPut("{id:int}/projects/{pid:int}", async (int id, int pid, [FromBody] LinkBody? body, HardwareService svc, CancellationToken ct) =>
            Results.Ok(await svc.LinkProjectAsync(id, pid, body?.Role, ct)));

        g.MapDelete("{id:int}/projects/{pid:int}", async (int id, int pid, HardwareService svc, CancellationToken ct) =>
        {
            await svc.UnlinkProjectAsync(id, pid, ct);
            return Results.NoContent();
        });

        // Config subresource
        g.MapGet("{id:int}/configs", async (int id, HttpRequest req, HardwareConfigService svc, CancellationToken ct) =>
        {
            var filter = new HardwareConfigListFilter(
                Kind: QueryHelpers.ParseEnum<HardwareConfigKind>(req, "kind"),
                Current: req.Query.ContainsKey("current") ? QueryHelpers.ParseBool(req, "current", true) : null,
                Limit: QueryHelpers.ParseInt(req, "limit", 50),
                Offset: QueryHelpers.ParseInt(req, "offset", 0));
            return Results.Ok(await svc.ListAsync(id, filter, ct));
        });
        g.MapPost("{id:int}/configs", async (int id, [FromBody] HardwareConfigCreateDto dto, HardwareConfigService svc, CancellationToken ct) =>
        {
            var created = await svc.RecordAsync(id, dto, ct);
            return Results.Created($"/api/hardware/{id}/configs/{created.Id}", created);
        });
        g.MapDelete("{id:int}/configs/{cid:int}", async (int id, int cid, HardwareConfigService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, cid, ct);
            return Results.NoContent();
        });

        // Loan subresource
        g.MapGet("{id:int}/loans", async (int id, LoanService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(id, ct)));
        g.MapPost("{id:int}/loans", async (int id, [FromBody] LoanCreateDto dto, LoanService svc, CancellationToken ct) =>
        {
            var created = await svc.StartAsync(id, dto, ct);
            return Results.Created($"/api/hardware/{id}/loans/{created.Id}", created);
        });
        g.MapPatch("{id:int}/loans/{lid:int}", async (int id, int lid, [FromBody] LoanUpdateDto dto, LoanService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateAsync(id, lid, dto, ct)));

        return app;
    }

    /// <summary>
    /// Reads a JSON Patch document (RFC 6902) from the request body. Accepts either
    /// <c>application/json-patch+json</c> or plain <c>application/json</c> with an
    /// operations array body — the latter keeps Scalar / curl easy to use.
    /// </summary>
    public static async Task<JsonPatchDocument> ReadPatchAsync(HttpRequest req, CancellationToken ct)
    {
        using var sr = new StreamReader(req.Body);
        var text = await sr.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(text)) throw new ValidationException("empty patch body");

        var ops = JsonSerializer.Deserialize<List<Operation>>(text,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (ops is null || ops.Count == 0) throw new ValidationException("patch body must be a JSON array of operations");

        var doc = new JsonPatchDocument();
        foreach (var op in ops) doc.Operations.Add(op);
        return doc;
    }

    public static void ValidatePatchAllowList(JsonPatchDocument doc, ISet<string> allowed)
    {
        foreach (var op in doc.Operations)
        {
            var path = op.path ?? "";
            // Allow drilling into JSON blobs like /specs/cpu — only the root segment must be allow-listed.
            var root = "/" + path.TrimStart('/').Split('/', 2)[0];
            if (!allowed.Contains(root))
                throw new PreconditionFailedException($"patch path '{path}' is not allowed");
        }
    }
}

/// <summary>
/// Apply patch operations field-by-field using a writer table. This bypasses the
/// reflection-based JsonPatch applier (which trips over snake/camel naming and
/// records) and gives us explicit, predictable behaviour.
/// </summary>
public static class PatchHelpers
{
    public static Task ApplyAsync<TEntity, TView>(
        JsonPatchDocument doc,
        TEntity entity,
        Func<TEntity, TView> getter,
        Dictionary<string, Action<TEntity, JsonElement?>> fieldWriters,
        CancellationToken ct)
    {
        foreach (var op in doc.Operations)
        {
            var path = (op.path ?? "").TrimStart('/');
            var first = path.Split('/', 2)[0];
            var key = "/" + first;
            if (!fieldWriters.TryGetValue(key, out var writer))
                throw new PreconditionFailedException($"patch path '/{path}' is not allowed");

            switch (op.OperationType)
            {
                case Microsoft.AspNetCore.JsonPatch.Operations.OperationType.Replace:
                case Microsoft.AspNetCore.JsonPatch.Operations.OperationType.Add:
                    writer(entity, JsonElementFromValue(op.value));
                    break;
                case Microsoft.AspNetCore.JsonPatch.Operations.OperationType.Remove:
                    writer(entity, null);
                    break;
                default:
                    throw new PreconditionFailedException($"patch op '{op.op}' not supported on '{key}'");
            }
        }
        return Task.CompletedTask;
    }

    private static JsonElement? JsonElementFromValue(object? value)
    {
        if (value is null) return null;
        if (value is JsonElement je) return je;
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return JsonDocument.Parse(json).RootElement;
    }
}

/// <summary>
/// Writer table for Hardware JSON Patch paths. Keep keys exactly aligned with
/// <see cref="HardwareService.PatchAllowList"/>.
/// </summary>
public static class HardwarePatchWriters
{
    private static string? StrOrNull(JsonElement? e) => e is null ? null : (e.Value.ValueKind == JsonValueKind.Null ? null : e.Value.GetString());
    private static DateTimeOffset? DateOrNull(JsonElement? e) =>
        e is null || e.Value.ValueKind == JsonValueKind.Null ? null :
        DateTimeOffset.TryParse(e.Value.GetString(), out var d) ? d : null;
    private static decimal? DecOrNull(JsonElement? e)
    {
        if (e is null || e.Value.ValueKind == JsonValueKind.Null) return null;
        return e.Value.ValueKind switch
        {
            JsonValueKind.Number => e.Value.GetDecimal(),
            JsonValueKind.String => decimal.TryParse(e.Value.GetString(), out var d) ? d : null,
            _ => null,
        };
    }
    private static JsonDocument? DocOrNull(JsonElement? e) =>
        e is null || e.Value.ValueKind == JsonValueKind.Null ? null : JsonDocument.Parse(e.Value.GetRawText());
    private static TEnum EnumOrThrow<TEnum>(JsonElement? e, string field) where TEnum : struct
    {
        if (e is null || e.Value.ValueKind != JsonValueKind.String)
            throw new PreconditionFailedException($"{field} must be a string enum value");
        var s = e.Value.GetString() ?? "";
        return Enum.TryParse<TEnum>(s, true, out var v) ? v : throw new PreconditionFailedException($"invalid {field}: {s}");
    }

    public static readonly Dictionary<string, Action<Models.Hardware, JsonElement?>> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/name"] = (h, e) => { var s = StrOrNull(e); if (string.IsNullOrWhiteSpace(s)) throw new ValidationException("name cannot be empty"); h.Name = s; },
        ["/manufacturer"] = (h, e) => h.Manufacturer = StrOrNull(e),
        ["/model"] = (h, e) => h.Model = StrOrNull(e),
        ["/serialNumber"] = (h, e) => h.SerialNumber = StrOrNull(e),
        ["/sku"] = (h, e) => h.Sku = StrOrNull(e),
        ["/assetTag"] = (h, e) => h.AssetTag = StrOrNull(e),
        ["/revision"] = (h, e) => h.Revision = StrOrNull(e),
        ["/identifiers"] = (h, e) => h.Identifiers = DocOrNull(e),
        ["/specs"] = (h, e) => h.Specs = DocOrNull(e),
        ["/links"] = (h, e) => h.Links = DocOrNull(e),
        ["/acquiredAt"] = (h, e) => h.AcquiredAt = DateOrNull(e),
        ["/purchasedFrom"] = (h, e) => h.PurchasedFrom = StrOrNull(e),
        ["/purchaseUrl"] = (h, e) => h.PurchaseUrl = StrOrNull(e),
        ["/cost"] = (h, e) => h.Cost = DecOrNull(e),
        ["/currency"] = (h, e) => h.Currency = StrOrNull(e),
        ["/warrantyExpiresAt"] = (h, e) => h.WarrantyExpiresAt = DateOrNull(e),
        ["/location"] = (h, e) => h.Location = StrOrNull(e),
        ["/condition"] = (h, e) => h.Condition = EnumOrThrow<HardwareCondition>(e, "condition"),
        ["/status"] = (h, e) => h.Status = EnumOrThrow<HardwareStatus>(e, "status"),
        ["/notes"] = (h, e) => h.Notes = StrOrNull(e),
    };
}
