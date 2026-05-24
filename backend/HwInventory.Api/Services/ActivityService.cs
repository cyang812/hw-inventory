using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Services;

/// <summary>
/// Owns all activity create/update/delete logic, including the LastUsedAt /
/// LastActivityAt sync that drives "idle" detection. Every transport (REST, gRPC,
/// MCP) calls these methods directly — there is no parallel implementation.
/// </summary>
public class ActivityService(AppDbContext db)
{
    public async Task<ActivityDto> CreateAsync(int hardwareId, ActivityCreateDto dto, CancellationToken ct)
    {
        var hw = await db.Hardware.IgnoreQueryFilters().FirstOrDefaultAsync(h => h.Id == hardwareId, ct)
            ?? throw new NotFoundException($"hardware {hardwareId}");

        if (dto.ProjectId is int pid)
        {
            var exists = await db.Projects.IgnoreQueryFilters().AnyAsync(p => p.Id == pid, ct);
            if (!exists) throw new NotFoundException($"project {pid}");
        }

        var occurredAt = (dto.OccurredAt ?? DateTimeOffset.UtcNow);
        var entity = new Activity
        {
            HardwareId = hardwareId,
            ProjectId = dto.ProjectId,
            Kind = dto.Kind,
            Description = dto.Description,
            Metadata = Mapping.FromElement(dto.Metadata),
            OccurredAt = occurredAt,
        };
        db.Activities.Add(entity);

        // Sync derived timestamps. We compare with the candidate timestamp so backdated
        // activities only bump if they become the new maximum.
        if (hw.LastActivityAt is null || occurredAt > hw.LastActivityAt)
            hw.LastActivityAt = occurredAt;
        if (ActivityKindMeta.UpdatesLastUsed(dto.Kind) && (hw.LastUsedAt is null || occurredAt > hw.LastUsedAt))
            hw.LastUsedAt = occurredAt;

        await db.SaveChangesAsync(ct);
        return Mapping.ToDto(entity);
    }

    public async Task<ListResponse<ActivityDto>> ListAsync(ActivityListFilter f, CancellationToken ct)
    {
        var query = db.Activities.AsNoTracking().AsQueryable();
        if (f.HardwareId is int hid) query = query.Where(a => a.HardwareId == hid);
        if (f.ProjectId is int pid) query = query.Where(a => a.ProjectId == pid);
        if (f.Kind is ActivityKind kind) query = query.Where(a => a.Kind == kind);
        if (f.OccurredAfter is { } oa) query = query.Where(a => a.OccurredAt >= oa);
        if (f.OccurredBefore is { } ob) query = query.Where(a => a.OccurredAt < ob);

        var total = await query.CountAsync(ct);
        query = (f.Sort ?? "-occurredAt") switch
        {
            "occurredAt" => query.OrderBy(a => a.OccurredAt),
            "-createdAt" => query.OrderByDescending(a => a.CreatedAt),
            "createdAt" => query.OrderBy(a => a.CreatedAt),
            _ => query.OrderByDescending(a => a.OccurredAt),
        };
        var items = await query
            .Skip(f.Offset).Take(f.Limit)
            .Select(a => new ActivityDto(a.Id, a.HardwareId, a.ProjectId, a.Kind,
                a.Description, a.Metadata == null ? (System.Text.Json.JsonElement?)null : a.Metadata.RootElement,
                a.OccurredAt, a.CreatedAt))
            .ToListAsync(ct);
        return new ListResponse<ActivityDto>(items, total, f.Limit, f.Offset);
    }

    /// <summary>
    /// Recompute LastUsedAt / LastActivityAt for a Hardware row from scratch.
    /// Used after delete or kind change.
    /// </summary>
    public async Task ResyncHardwareTimestampsAsync(int hardwareId, CancellationToken ct)
    {
        var hw = await db.Hardware.IgnoreQueryFilters().FirstOrDefaultAsync(h => h.Id == hardwareId, ct);
        if (hw is null) return;
        var acts = await db.Activities.Where(a => a.HardwareId == hardwareId).ToListAsync(ct);
        hw.LastActivityAt = acts.Count == 0 ? null : acts.Max(a => a.OccurredAt);
        hw.LastUsedAt = acts.Where(a => ActivityKindMeta.UpdatesLastUsed(a.Kind))
                            .Select(a => (DateTimeOffset?)a.OccurredAt)
                            .DefaultIfEmpty(null)
                            .Max();
        await db.SaveChangesAsync(ct);
    }
}
