using System.Globalization;
using System.Text;
using System.Text.Json;
using HwInventory.Api.Data;
using HwInventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Services;

/// <summary>
/// JSON dump/restore + CSV export for spot-checking. All operations work in terms
/// of the public entity shape, so a JSON dump from a dev SQLite DB is loadable
/// into a production Postgres DB without conversion.
/// </summary>
public class ImportExportService(AppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        // Hardware ↔ Categories and Hardware ↔ Tags are bidirectional EF navigations;
        // without this, ExportJsonAsync blows up with "object cycle detected" on any
        // hardware that has at least one category or tag linked.
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        // Emit enums as their string names so the dump is human-readable AND so
        // ImportJsonAsync can parse them back via Enum.Parse(...).
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static T ParseEnumFlexible<T>(JsonElement el, T fallback) where T : struct, Enum =>
        el.ValueKind switch
        {
            JsonValueKind.String => Enum.TryParse<T>(el.GetString(), ignoreCase: true, out var v) ? v : fallback,
            JsonValueKind.Number when el.TryGetInt32(out var n) && Enum.IsDefined(typeof(T), n) => (T)Enum.ToObject(typeof(T), n),
            _ => fallback,
        };

    public async Task<string> ExportJsonAsync(CancellationToken ct)
    {
        var dump = new
        {
            categories = await db.Categories.AsNoTracking().ToListAsync(ct),
            tags = await db.Tags.AsNoTracking().ToListAsync(ct),
            hardware = await db.Hardware.IgnoreQueryFilters().AsNoTracking()
                .Include(h => h.Categories).Include(h => h.Tags).ToListAsync(ct),
            projects = await db.Projects.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
            hardware_projects = await db.HardwareProjects.AsNoTracking().ToListAsync(ct),
            activities = await db.Activities.AsNoTracking().ToListAsync(ct),
            hardware_configs = await db.HardwareConfigs.AsNoTracking().ToListAsync(ct),
            loans = await db.Loans.AsNoTracking().ToListAsync(ct),
        };
        return JsonSerializer.Serialize(dump, JsonOpts);
    }

    public async Task<int> ImportJsonAsync(Stream body, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(body, default, ct);
        var root = doc.RootElement;

        var idMaps = new IdMap();

        if (root.TryGetProperty("categories", out var cats))
        {
            foreach (var c in cats.EnumerateArray())
            {
                var slug = c.GetProperty("slug").GetString() ?? "";
                var existing = await db.Categories.FirstOrDefaultAsync(x => x.Slug == slug, ct);
                if (existing != null) { idMaps.Category[c.GetProperty("id").GetInt32()] = existing.Id; continue; }
                var entity = new Category
                {
                    Name = c.GetProperty("name").GetString() ?? slug,
                    Slug = slug,
                    Icon = c.TryGetProperty("icon", out var icn) ? icn.GetString() : null,
                    Description = c.TryGetProperty("description", out var d) ? d.GetString() : null,
                };
                db.Categories.Add(entity);
                await db.SaveChangesAsync(ct);
                idMaps.Category[c.GetProperty("id").GetInt32()] = entity.Id;
            }
        }

        if (root.TryGetProperty("tags", out var tags))
        {
            foreach (var t in tags.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString() ?? "";
                var existing = await db.Tags.FirstOrDefaultAsync(x => x.Name == name, ct);
                if (existing != null) { idMaps.Tag[t.GetProperty("id").GetInt32()] = existing.Id; continue; }
                var entity = new Tag { Name = name, Color = t.TryGetProperty("color", out var col) ? col.GetString() : null };
                db.Tags.Add(entity);
                await db.SaveChangesAsync(ct);
                idMaps.Tag[t.GetProperty("id").GetInt32()] = entity.Id;
            }
        }

        var imported = 0;
        if (root.TryGetProperty("hardware", out var hws))
        {
            foreach (var h in hws.EnumerateArray())
            {
                var entity = ReadHardware(h, idMaps);
                db.Hardware.Add(entity);
                await db.SaveChangesAsync(ct);
                idMaps.Hardware[h.GetProperty("id").GetInt32()] = entity.Id;
                await LinkHardwareCategoriesAndTagsAsync(entity, h, idMaps, ct);
                imported++;
            }
        }

        if (root.TryGetProperty("projects", out var projs))
        {
            foreach (var p in projs.EnumerateArray())
            {
                var entity = ReadProject(p);
                db.Projects.Add(entity);
                await db.SaveChangesAsync(ct);
                idMaps.Project[p.GetProperty("id").GetInt32()] = entity.Id;
            }
        }

        if (root.TryGetProperty("hardware_projects", out var hps))
        {
            foreach (var hp in hps.EnumerateArray())
            {
                var hid = idMaps.Hardware.GetValueOrDefault(hp.GetProperty("hardwareId").GetInt32());
                var pid = idMaps.Project.GetValueOrDefault(hp.GetProperty("projectId").GetInt32());
                if (hid == 0 || pid == 0) continue;
                var exists = await db.HardwareProjects.AnyAsync(x => x.HardwareId == hid && x.ProjectId == pid, ct);
                if (exists) continue;
                db.HardwareProjects.Add(new HardwareProject
                {
                    HardwareId = hid,
                    ProjectId = pid,
                    Role = hp.TryGetProperty("role", out var r) ? r.GetString() : null,
                });
            }
            await db.SaveChangesAsync(ct);
        }

        if (root.TryGetProperty("activities", out var acts))
        {
            foreach (var a in acts.EnumerateArray())
            {
                var hid = idMaps.Hardware.GetValueOrDefault(a.GetProperty("hardwareId").GetInt32());
                if (hid == 0) continue;
                int? pid = a.TryGetProperty("projectId", out var pidEl) && pidEl.ValueKind == JsonValueKind.Number
                    ? idMaps.Project.GetValueOrDefault(pidEl.GetInt32()) : null;
                if (pid == 0) pid = null;
                db.Activities.Add(new Activity
                {
                    HardwareId = hid,
                    ProjectId = pid,
                    Kind = ParseEnumFlexible(a.GetProperty("kind"), ActivityKind.Used),
                    Description = a.TryGetProperty("description", out var ds) ? ds.GetString() : null,
                    Metadata = a.TryGetProperty("metadata", out var m) && m.ValueKind != JsonValueKind.Null
                                ? JsonDocument.Parse(m.GetRawText()) : null,
                    OccurredAt = a.GetProperty("occurredAt").GetDateTimeOffset(),
                });
            }
            await db.SaveChangesAsync(ct);
        }

        if (root.TryGetProperty("hardware_configs", out var cfgs))
        {
            foreach (var c in cfgs.EnumerateArray())
            {
                var hid = idMaps.Hardware.GetValueOrDefault(c.GetProperty("hardwareId").GetInt32());
                if (hid == 0) continue;
                db.HardwareConfigs.Add(new HardwareConfig
                {
                    HardwareId = hid,
                    Kind = ParseEnumFlexible(c.GetProperty("kind"), HardwareConfigKind.Config),
                    Name = c.GetProperty("name").GetString() ?? "",
                    Version = c.TryGetProperty("version", out var v) ? v.GetString() : null,
                    Notes = c.TryGetProperty("notes", out var n) ? n.GetString() : null,
                    InstalledAt = c.TryGetProperty("installedAt", out var ia) && ia.ValueKind != JsonValueKind.Null ? ia.GetDateTimeOffset() : null,
                    IsCurrent = c.TryGetProperty("isCurrent", out var ic) && ic.GetBoolean(),
                });
            }
            await db.SaveChangesAsync(ct);
        }

        if (root.TryGetProperty("loans", out var loans))
        {
            foreach (var l in loans.EnumerateArray())
            {
                var hid = idMaps.Hardware.GetValueOrDefault(l.GetProperty("hardwareId").GetInt32());
                if (hid == 0) continue;
                db.Loans.Add(new Loan
                {
                    HardwareId = hid,
                    LoanedTo = l.GetProperty("loanedTo").GetString() ?? "",
                    LoanedAt = l.GetProperty("loanedAt").GetDateTimeOffset(),
                    DueAt = l.TryGetProperty("dueAt", out var da) && da.ValueKind != JsonValueKind.Null ? da.GetDateTimeOffset() : null,
                    ReturnedAt = l.TryGetProperty("returnedAt", out var ra) && ra.ValueKind != JsonValueKind.Null ? ra.GetDateTimeOffset() : null,
                    Notes = l.TryGetProperty("notes", out var nn) ? nn.GetString() : null,
                });
            }
            await db.SaveChangesAsync(ct);
        }

        return imported;
    }

    public async Task<string> ExportCsvAsync(string entity, CancellationToken ct)
    {
        return entity.ToLowerInvariant() switch
        {
            "hardware" => HardwareCsv(await db.Hardware.IgnoreQueryFilters().AsNoTracking()
                .Include(h => h.Categories).Include(h => h.Tags).ToListAsync(ct)),
            "activities" => ActivitiesCsv(await db.Activities.AsNoTracking().ToListAsync(ct)),
            "projects" => ProjectsCsv(await db.Projects.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct)),
            _ => throw new ValidationException($"unknown entity '{entity}' (use hardware|activities|projects)"),
        };
    }

    private static string HardwareCsv(List<Hardware> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,name,manufacturer,model,status,condition,location,last_used_at,last_activity_at,categories,tags");
        foreach (var h in rows)
        {
            sb.AppendLine(string.Join(",",
                h.Id,
                Csv(h.Name),
                Csv(h.Manufacturer),
                Csv(h.Model),
                h.Status,
                h.Condition,
                Csv(h.Location),
                h.LastUsedAt?.ToString("o", CultureInfo.InvariantCulture),
                h.LastActivityAt?.ToString("o", CultureInfo.InvariantCulture),
                Csv(string.Join("|", h.Categories.Select(c => c.Slug))),
                Csv(string.Join("|", h.Tags.Select(t => t.Name)))));
        }
        return sb.ToString();
    }

    private static string ActivitiesCsv(List<Activity> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,hardware_id,project_id,kind,description,occurred_at");
        foreach (var a in rows)
        {
            sb.AppendLine(string.Join(",",
                a.Id, a.HardwareId,
                a.ProjectId?.ToString() ?? "",
                a.Kind,
                Csv(a.Description),
                a.OccurredAt.ToString("o", CultureInfo.InvariantCulture)));
        }
        return sb.ToString();
    }

    private static string ProjectsCsv(List<Project> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,title,slug,status,priority,started_at,target_date,completed_at,archived_at");
        foreach (var p in rows)
        {
            sb.AppendLine(string.Join(",",
                p.Id,
                Csv(p.Title),
                Csv(p.Slug),
                p.Status, p.Priority,
                p.StartedAt?.ToString("o", CultureInfo.InvariantCulture),
                p.TargetDate?.ToString("o", CultureInfo.InvariantCulture),
                p.CompletedAt?.ToString("o", CultureInfo.InvariantCulture),
                p.ArchivedAt?.ToString("o", CultureInfo.InvariantCulture)));
        }
        return sb.ToString();
    }

    private static string Csv(string? v)
    {
        if (v is null) return "";
        var needsQuote = v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r');
        if (!needsQuote) return v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }

    private static Hardware ReadHardware(JsonElement h, IdMap maps)
    {
        var entity = new Hardware
        {
            Name = h.GetProperty("name").GetString() ?? "",
            Manufacturer = h.TryGetProperty("manufacturer", out var m) ? m.GetString() : null,
            Model = h.TryGetProperty("model", out var mo) ? mo.GetString() : null,
            SerialNumber = h.TryGetProperty("serialNumber", out var sn) ? sn.GetString() : null,
            Sku = h.TryGetProperty("sku", out var sk) ? sk.GetString() : null,
            AssetTag = h.TryGetProperty("assetTag", out var at) ? at.GetString() : null,
            Revision = h.TryGetProperty("revision", out var rv) ? rv.GetString() : null,
            Identifiers = h.TryGetProperty("identifiers", out var id) && id.ValueKind != JsonValueKind.Null ? JsonDocument.Parse(id.GetRawText()) : null,
            Specs = h.TryGetProperty("specs", out var sp) && sp.ValueKind != JsonValueKind.Null ? JsonDocument.Parse(sp.GetRawText()) : null,
            Links = h.TryGetProperty("links", out var lk) && lk.ValueKind != JsonValueKind.Null ? JsonDocument.Parse(lk.GetRawText()) : null,
            AcquiredAt = h.TryGetProperty("acquiredAt", out var aa) && aa.ValueKind != JsonValueKind.Null ? aa.GetDateTimeOffset() : null,
            PurchasedFrom = h.TryGetProperty("purchasedFrom", out var pf) ? pf.GetString() : null,
            PurchaseUrl = h.TryGetProperty("purchaseUrl", out var pu) ? pu.GetString() : null,
            Cost = h.TryGetProperty("cost", out var co) && co.ValueKind == JsonValueKind.Number ? co.GetDecimal() : null,
            Currency = h.TryGetProperty("currency", out var cu) ? cu.GetString() : null,
            WarrantyExpiresAt = h.TryGetProperty("warrantyExpiresAt", out var we) && we.ValueKind != JsonValueKind.Null ? we.GetDateTimeOffset() : null,
            Location = h.TryGetProperty("location", out var lo) ? lo.GetString() : null,
            Status = h.TryGetProperty("status", out var st) ? ParseEnumFlexible(st, HardwareStatus.Available) : HardwareStatus.Available,
            Condition = h.TryGetProperty("condition", out var cd) ? ParseEnumFlexible(cd, HardwareCondition.Unknown) : HardwareCondition.Unknown,
            Notes = h.TryGetProperty("notes", out var no) ? no.GetString() : null,
            LastUsedAt = h.TryGetProperty("lastUsedAt", out var lu) && lu.ValueKind != JsonValueKind.Null ? lu.GetDateTimeOffset() : null,
            LastActivityAt = h.TryGetProperty("lastActivityAt", out var la) && la.ValueKind != JsonValueKind.Null ? la.GetDateTimeOffset() : null,
            ArchivedAt = h.TryGetProperty("archivedAt", out var ar) && ar.ValueKind != JsonValueKind.Null ? ar.GetDateTimeOffset() : null,
        };
        return entity;
    }

    private static Project ReadProject(JsonElement p)
    {
        return new Project
        {
            Title = p.GetProperty("title").GetString() ?? "",
            Slug = p.GetProperty("slug").GetString() ?? "",
            Description = p.TryGetProperty("description", out var d) ? d.GetString() : null,
            Status = p.TryGetProperty("status", out var s) ? ParseEnumFlexible(s, ProjectStatus.Idea) : ProjectStatus.Idea,
            Priority = p.TryGetProperty("priority", out var pr) ? ParseEnumFlexible(pr, ProjectPriority.Medium) : ProjectPriority.Medium,
            StartedAt = p.TryGetProperty("startedAt", out var sa) && sa.ValueKind != JsonValueKind.Null ? sa.GetDateTimeOffset() : null,
            TargetDate = p.TryGetProperty("targetDate", out var td) && td.ValueKind != JsonValueKind.Null ? td.GetDateTimeOffset() : null,
            CompletedAt = p.TryGetProperty("completedAt", out var ca) && ca.ValueKind != JsonValueKind.Null ? ca.GetDateTimeOffset() : null,
            Notes = p.TryGetProperty("notes", out var n) ? n.GetString() : null,
            Links = p.TryGetProperty("links", out var lk) && lk.ValueKind != JsonValueKind.Null ? JsonDocument.Parse(lk.GetRawText()) : null,
            ArchivedAt = p.TryGetProperty("archivedAt", out var ar) && ar.ValueKind != JsonValueKind.Null ? ar.GetDateTimeOffset() : null,
        };
    }

    private async Task LinkHardwareCategoriesAndTagsAsync(
        Hardware entity, JsonElement h, IdMap maps, CancellationToken ct)
    {
        // Collect distinct category & tag IDs from either of the two supported shapes:
        //  - flat: { "categoryIds": [1,2], "tagIds": [3] }  (hand-authored imports)
        //  - nested: { "categories": [{id:1,...}], "tags": [{id:3,...}] }  (round-trip from /api/export/json)
        // Inputs are the *source* IDs from the import file; map them via idMaps to the real DB IDs.
        var catSourceIds = new HashSet<int>();
        var tagSourceIds = new HashSet<int>();

        if (h.TryGetProperty("categoryIds", out var cids) && cids.ValueKind == JsonValueKind.Array)
            foreach (var v in cids.EnumerateArray())
                if (v.ValueKind == JsonValueKind.Number) catSourceIds.Add(v.GetInt32());

        if (h.TryGetProperty("tagIds", out var tids) && tids.ValueKind == JsonValueKind.Array)
            foreach (var v in tids.EnumerateArray())
                if (v.ValueKind == JsonValueKind.Number) tagSourceIds.Add(v.GetInt32());

        if (h.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
            foreach (var v in cats.EnumerateArray())
                if (v.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                    catSourceIds.Add(idEl.GetInt32());

        if (h.TryGetProperty("tags", out var tgs) && tgs.ValueKind == JsonValueKind.Array)
            foreach (var v in tgs.EnumerateArray())
                if (v.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                    tagSourceIds.Add(idEl.GetInt32());

        if (catSourceIds.Count == 0 && tagSourceIds.Count == 0) return;

        var realCatIds = catSourceIds
            .Select(src => maps.Category.TryGetValue(src, out var real) ? real : 0)
            .Where(id => id != 0).Distinct().ToList();
        var realTagIds = tagSourceIds
            .Select(src => maps.Tag.TryGetValue(src, out var real) ? real : 0)
            .Where(id => id != 0).Distinct().ToList();

        if (realCatIds.Count > 0)
        {
            var loaded = await db.Hardware
                .Include(x => x.Categories)
                .FirstAsync(x => x.Id == entity.Id, ct);
            var existing = loaded.Categories.Select(c => c.Id).ToHashSet();
            var toAdd = await db.Categories
                .Where(c => realCatIds.Contains(c.Id) && !existing.Contains(c.Id))
                .ToListAsync(ct);
            foreach (var c in toAdd) loaded.Categories.Add(c);
        }

        if (realTagIds.Count > 0)
        {
            var loaded = await db.Hardware
                .Include(x => x.Tags)
                .FirstAsync(x => x.Id == entity.Id, ct);
            var existing = loaded.Tags.Select(t => t.Id).ToHashSet();
            var toAdd = await db.Tags
                .Where(t => realTagIds.Contains(t.Id) && !existing.Contains(t.Id))
                .ToListAsync(ct);
            foreach (var t in toAdd) loaded.Tags.Add(t);
        }

        if (realCatIds.Count > 0 || realTagIds.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private class IdMap
    {
        public Dictionary<int, int> Category { get; } = [];
        public Dictionary<int, int> Tag { get; } = [];
        public Dictionary<int, int> Hardware { get; } = [];
        public Dictionary<int, int> Project { get; } = [];
    }
}
