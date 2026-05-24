using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Services;

public class HardwareService(AppDbContext db, ActivityService activities)
{
    private IQueryable<Hardware> FullQuery(bool includeArchived) =>
        (includeArchived ? db.Hardware.IgnoreQueryFilters() : db.Hardware)
            .Include(h => h.Categories)
            .Include(h => h.Tags);

    private static IQueryable<Hardware> ApplyHardwareSort(IQueryable<Hardware> q, string? sort) => sort switch
    {
        "name" => q.OrderBy(h => h.Name),
        "-name" => q.OrderByDescending(h => h.Name),
        "createdAt" => q.OrderBy(h => h.CreatedAt),
        "-createdAt" => q.OrderByDescending(h => h.CreatedAt),
        "updatedAt" => q.OrderBy(h => h.UpdatedAt),
        "-updatedAt" => q.OrderByDescending(h => h.UpdatedAt),
        "lastUsedAt" => q.OrderBy(h => h.LastUsedAt),
        "-lastUsedAt" => q.OrderByDescending(h => h.LastUsedAt),
        _ => q.OrderBy(h => h.Name),
    };

    public async Task<ListResponse<HardwareSummaryDto>> ListAsync(HardwareListFilter f, CancellationToken ct)
    {
        var query = FullQuery(f.IncludeArchived).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var like = $"%{f.Q.Trim()}%";
            query = query.Where(h =>
                EF.Functions.Like(h.Name, like) ||
                (h.Model != null && EF.Functions.Like(h.Model, like)) ||
                (h.Manufacturer != null && EF.Functions.Like(h.Manufacturer, like)) ||
                (h.Notes != null && EF.Functions.Like(h.Notes, like)) ||
                (h.SerialNumber != null && EF.Functions.Like(h.SerialNumber, like)));
        }
        if (f.Status is HardwareStatus s) query = query.Where(h => h.Status == s);
        if (f.Condition is HardwareCondition c) query = query.Where(h => h.Condition == c);
        if (f.CreatedAfter is { } ca) query = query.Where(h => h.CreatedAt >= ca);
        if (f.CreatedBefore is { } cb) query = query.Where(h => h.CreatedAt < cb);
        if (f.UpdatedAfter is { } ua) query = query.Where(h => h.UpdatedAt >= ua);
        if (f.UpdatedBefore is { } ub) query = query.Where(h => h.UpdatedAt < ub);

        // Multi-category & multi-tag are AND (item must carry every requested id).
        if (f.CategoryIds is { Count: > 0 })
        {
            foreach (var cid in f.CategoryIds.Distinct())
            {
                var localId = cid;
                query = query.Where(h => h.Categories.Any(c => c.Id == localId));
            }
        }
        if (f.TagIds is { Count: > 0 })
        {
            foreach (var tid in f.TagIds.Distinct())
            {
                var localId = tid;
                query = query.Where(h => h.Tags.Any(t => t.Id == localId));
            }
        }

        if (f.IdleDays is int days && days > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            query = query.Where(h =>
                (h.Status == HardwareStatus.Available || h.Status == HardwareStatus.InUse) &&
                h.ArchivedAt == null &&
                (h.LastUsedAt == null ? h.CreatedAt : h.LastUsedAt) < cutoff);
        }

        var total = await query.CountAsync(ct);
        query = ApplyHardwareSort(query, f.Sort);
        var items = await query.Skip(f.Offset).Take(f.Limit).ToListAsync(ct);
        return new ListResponse<HardwareSummaryDto>(items.Select(Mapping.ToSummary).ToList(), total, f.Limit, f.Offset);
    }

    public async Task<HardwareDto> GetAsync(int id, bool includeArchived, CancellationToken ct)
    {
        var q = (includeArchived ? db.Hardware.IgnoreQueryFilters() : db.Hardware)
            .Include(h => h.Categories)
            .Include(h => h.Tags)
            .Include(h => h.HardwareProjects).ThenInclude(hp => hp.Project)
            .Include(h => h.Configs)
            .Include(h => h.Loans)
            .Include(h => h.Activities);
        var h = await q.AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"hardware {id}");
        return Mapping.ToDto(h);
    }

    public async Task<HardwareDto> CreateAsync(HardwareCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("name is required");

        var h = new Hardware
        {
            Name = dto.Name.Trim(),
            Manufacturer = dto.Manufacturer,
            Model = dto.Model,
            SerialNumber = dto.SerialNumber,
            Sku = dto.Sku,
            AssetTag = dto.AssetTag,
            Revision = dto.Revision,
            Identifiers = Mapping.FromElement(dto.Identifiers),
            Specs = Mapping.FromElement(dto.Specs),
            Links = Mapping.FromElement(dto.Links),
            AcquiredAt = dto.AcquiredAt,
            PurchasedFrom = dto.PurchasedFrom,
            PurchaseUrl = dto.PurchaseUrl,
            Cost = dto.Cost,
            Currency = dto.Currency,
            WarrantyExpiresAt = dto.WarrantyExpiresAt,
            Location = dto.Location,
            Condition = dto.Condition,
            Status = dto.Status,
            Notes = dto.Notes,
        };

        if (dto.CategoryIds is { Count: > 0 })
        {
            var cats = await db.Categories.Where(c => dto.CategoryIds.Contains(c.Id)).ToListAsync(ct);
            foreach (var c in cats) h.Categories.Add(c);
        }
        if (dto.TagIds is { Count: > 0 })
        {
            var tags = await db.Tags.Where(t => dto.TagIds.Contains(t.Id)).ToListAsync(ct);
            foreach (var t in tags) h.Tags.Add(t);
        }

        db.Hardware.Add(h);
        await db.SaveChangesAsync(ct);
        return await GetAsync(h.Id, includeArchived: false, ct);
    }

    /// <summary>
    /// Allow-list of paths the JSON Patch document can touch on Hardware.
    /// Anything outside this list is rejected with a 422.
    /// </summary>
    public static readonly HashSet<string> PatchAllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "/name", "/manufacturer", "/model",
        "/serialNumber", "/sku", "/assetTag", "/revision",
        "/identifiers", "/specs", "/links",
        "/acquiredAt", "/purchasedFrom", "/purchaseUrl",
        "/cost", "/currency", "/warrantyExpiresAt",
        "/location", "/condition", "/status",
        "/notes",
    };

    public async Task<HardwareDto> ArchiveAsync(int id, CancellationToken ct)
    {
        var h = await db.Hardware.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"hardware {id}");
        if (h.ArchivedAt is null)
        {
            h.ArchivedAt = DateTimeOffset.UtcNow;
            h.Status = HardwareStatus.Archived;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(h.Id, includeArchived: true, ct);
    }

    public async Task<HardwareDto> UnarchiveAsync(int id, CancellationToken ct)
    {
        var h = await db.Hardware.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"hardware {id}");
        h.ArchivedAt = null;
        if (h.Status == HardwareStatus.Archived) h.Status = HardwareStatus.Available;
        await db.SaveChangesAsync(ct);
        return await GetAsync(h.Id, includeArchived: true, ct);
    }

    public async Task HardDeleteAsync(int id, CancellationToken ct)
    {
        var h = await db.Hardware.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"hardware {id}");
        db.Hardware.Remove(h);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Category / Tag linkage ----------

    public async Task LinkCategoryAsync(int hardwareId, int categoryId, CancellationToken ct)
    {
        var h = await db.Hardware.IgnoreQueryFilters()
            .Include(x => x.Categories)
            .FirstOrDefaultAsync(x => x.Id == hardwareId, ct)
            ?? throw new NotFoundException($"hardware {hardwareId}");
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == categoryId, ct)
            ?? throw new NotFoundException($"category {categoryId}");
        if (h.Categories.Any(x => x.Id == categoryId)) return;
        h.Categories.Add(c);
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlinkCategoryAsync(int hardwareId, int categoryId, CancellationToken ct)
    {
        var h = await db.Hardware.IgnoreQueryFilters()
            .Include(x => x.Categories)
            .FirstOrDefaultAsync(x => x.Id == hardwareId, ct)
            ?? throw new NotFoundException($"hardware {hardwareId}");
        var existing = h.Categories.FirstOrDefault(x => x.Id == categoryId);
        if (existing is null) return;
        h.Categories.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task LinkTagAsync(int hardwareId, int tagId, CancellationToken ct)
    {
        var h = await db.Hardware.IgnoreQueryFilters()
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == hardwareId, ct)
            ?? throw new NotFoundException($"hardware {hardwareId}");
        var t = await db.Tags.FirstOrDefaultAsync(x => x.Id == tagId, ct)
            ?? throw new NotFoundException($"tag {tagId}");
        if (h.Tags.Any(x => x.Id == tagId)) return;
        h.Tags.Add(t);
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlinkTagAsync(int hardwareId, int tagId, CancellationToken ct)
    {
        var h = await db.Hardware.IgnoreQueryFilters()
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == hardwareId, ct)
            ?? throw new NotFoundException($"hardware {hardwareId}");
        var existing = h.Tags.FirstOrDefault(x => x.Id == tagId);
        if (existing is null) return;
        h.Tags.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Project linkage ----------

    public async Task<HardwareProjectLinkDto> LinkProjectAsync(int hardwareId, int projectId, string? role, CancellationToken ct)
    {
        var hwExists = await db.Hardware.IgnoreQueryFilters().AnyAsync(h => h.Id == hardwareId, ct);
        if (!hwExists) throw new NotFoundException($"hardware {hardwareId}");
        var project = await db.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new NotFoundException($"project {projectId}");

        var link = await db.HardwareProjects.FirstOrDefaultAsync(hp => hp.HardwareId == hardwareId && hp.ProjectId == projectId, ct);
        if (link is null)
        {
            link = new HardwareProject { HardwareId = hardwareId, ProjectId = projectId, Role = role };
            db.HardwareProjects.Add(link);
        }
        else if (role is not null)
        {
            link.Role = role;
        }
        await db.SaveChangesAsync(ct);
        return new HardwareProjectLinkDto(projectId, project.Title, link.Role, link.CreatedAt);
    }

    public async Task UnlinkProjectAsync(int hardwareId, int projectId, CancellationToken ct)
    {
        var link = await db.HardwareProjects.FirstOrDefaultAsync(hp => hp.HardwareId == hardwareId && hp.ProjectId == projectId, ct);
        if (link is null) return;
        db.HardwareProjects.Remove(link);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Quick action ----------

    public async Task<ActivityDto> MarkUsedTodayAsync(int hardwareId, CancellationToken ct) =>
        await activities.CreateAsync(hardwareId,
            new ActivityCreateDto(ActivityKind.Used, "Marked as used today"),
            ct);
}
