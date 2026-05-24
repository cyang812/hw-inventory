using HwInventory.Api.Config;
using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Services;

public class DashboardService(AppDbContext db, HwInventoryOptions opts, HardwareService hardware)
{
    public async Task<DashboardStatsDto> StatsAsync(CancellationToken ct)
    {
        var hardwareTotal = await db.Hardware.IgnoreQueryFilters().CountAsync(ct);
        var archived = await db.Hardware.IgnoreQueryFilters().CountAsync(h => h.ArchivedAt != null, ct);
        var projectsTotal = await db.Projects.IgnoreQueryFilters().CountAsync(ct);

        var since = DateTimeOffset.UtcNow.AddDays(-30);
        var activitiesLast30 = await db.Activities.CountAsync(a => a.OccurredAt >= since, ct);

        var byStatus = await db.Hardware.GroupBy(h => h.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byCondition = await db.Hardware.GroupBy(h => h.Condition)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byCategoryRows = await db.Hardware
            .SelectMany(h => h.Categories, (h, c) => c.Name)
            .GroupBy(n => n)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var projectsByStatus = await db.Projects.GroupBy(p => p.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var idle = await ListIdleAsync(opts.IdleDefaultDays, 1, 0, ct);
        var now = DateTimeOffset.UtcNow;
        var overdueCount = await db.Loans
            .Where(l => l.ReturnedAt == null && l.DueAt != null && l.DueAt < now)
            .CountAsync(ct);

        return new DashboardStatsDto(
            hardwareTotal, archived, projectsTotal, activitiesLast30,
            idle.Total, overdueCount,
            byStatus.ToDictionary(x => EnumKey(x.Key), x => x.Count),
            byCondition.ToDictionary(x => EnumKey(x.Key), x => x.Count),
            byCategoryRows.ToDictionary(x => x.Key, x => x.Count),
            projectsByStatus.ToDictionary(x => EnumKey(x.Key), x => x.Count));
    }

    /// <summary>Format an enum value as camelCase string (matches REST enum serialization).</summary>
    private static string EnumKey<TEnum>(TEnum value) where TEnum : Enum
    {
        var s = value.ToString();
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToLowerInvariant(s[0]) + s[1..];
    }

    public async Task<ListResponse<HardwareSummaryDto>> ListIdleAsync(int days, int limit, int offset, CancellationToken ct)
    {
        var filter = new HardwareListFilter(Limit: limit, Offset: offset, Sort: "lastUsedAt", IdleDays: days);
        return await hardware.ListAsync(filter, ct);
    }

    public async Task<ListResponse<HardwareSummaryDto>> ListSuggestionsAsync(int days, int limit, int offset, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var query = db.Hardware
            .Include(h => h.Categories)
            .Include(h => h.Tags)
            .AsNoTracking()
            .Where(h =>
                (h.Status == HardwareStatus.Available || h.Status == HardwareStatus.InUse) &&
                h.ArchivedAt == null &&
                (h.LastUsedAt == null ? h.CreatedAt : h.LastUsedAt) < cutoff &&
                !h.HardwareProjects.Any(hp =>
                    hp.Project!.ArchivedAt == null &&
                    (hp.Project.Status == ProjectStatus.Idea ||
                     hp.Project.Status == ProjectStatus.Planned ||
                     hp.Project.Status == ProjectStatus.InProgress)));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(h => h.LastUsedAt)
            .Skip(offset).Take(limit)
            .ToListAsync(ct);
        return new ListResponse<HardwareSummaryDto>(items.Select(Mapping.ToSummary).ToList(), total, limit, offset);
    }

    public async Task<ListResponse<LoanDto>> ListOverdueLoansAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var items = await db.Loans.AsNoTracking()
            .Where(l => l.ReturnedAt == null && l.DueAt != null && l.DueAt < now)
            .OrderBy(l => l.DueAt)
            .ToListAsync(ct);
        return new ListResponse<LoanDto>(items.Select(Mapping.ToDto).ToList(), items.Count, items.Count, 0);
    }
}
