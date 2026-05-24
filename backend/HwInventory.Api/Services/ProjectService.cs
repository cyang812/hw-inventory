using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Services;

public class ProjectService(AppDbContext db)
{
    public static readonly HashSet<string> PatchAllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "/title", "/slug", "/description",
        "/status", "/priority",
        "/startedAt", "/targetDate", "/completedAt",
        "/notes", "/links",
    };

    public async Task<ListResponse<ProjectSummaryDto>> ListAsync(ProjectListFilter f, CancellationToken ct)
    {
        var query = (f.IncludeArchived ? db.Projects.IgnoreQueryFilters() : db.Projects).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var like = $"%{f.Q.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Title, like) ||
                                     (p.Description != null && EF.Functions.Like(p.Description, like)));
        }
        if (f.Status is ProjectStatus s) query = query.Where(p => p.Status == s);
        if (f.Priority is ProjectPriority pr) query = query.Where(p => p.Priority == pr);
        if (f.CreatedAfter is { } ca) query = query.Where(p => p.CreatedAt >= ca);
        if (f.CreatedBefore is { } cb) query = query.Where(p => p.CreatedAt < cb);

        var total = await query.CountAsync(ct);

        query = (f.Sort ?? "title") switch
        {
            "-title" => query.OrderByDescending(p => p.Title),
            "createdAt" => query.OrderBy(p => p.CreatedAt),
            "-createdAt" => query.OrderByDescending(p => p.CreatedAt),
            "updatedAt" => query.OrderBy(p => p.UpdatedAt),
            "-updatedAt" => query.OrderByDescending(p => p.UpdatedAt),
            _ => query.OrderBy(p => p.Title),
        };

        var rows = await query
            .Skip(f.Offset).Take(f.Limit)
            .Select(p => new { Project = p, HardwareCount = p.HardwareProjects.Count })
            .ToListAsync(ct);
        var items = rows.Select(r => Mapping.ToSummary(r.Project, r.HardwareCount)).ToList();
        return new ListResponse<ProjectSummaryDto>(items, total, f.Limit, f.Offset);
    }

    public async Task<ProjectDto> GetAsync(int id, bool includeArchived, CancellationToken ct)
    {
        var q = (includeArchived ? db.Projects.IgnoreQueryFilters() : db.Projects)
            .Include(p => p.HardwareProjects).ThenInclude(hp => hp.Hardware);
        var p = await q.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"project {id}");
        return Mapping.ToDto(p);
    }

    public async Task<ProjectDto> CreateAsync(ProjectCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ValidationException("title is required");
        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? Mapping.Slugify(dto.Title) : dto.Slug!.Trim();
        if (await db.Projects.IgnoreQueryFilters().AnyAsync(p => p.Slug == slug, ct))
            throw new ConflictException($"project with slug '{slug}' already exists");

        var p = new Project
        {
            Title = dto.Title.Trim(),
            Slug = slug,
            Description = dto.Description,
            Status = dto.Status,
            Priority = dto.Priority,
            StartedAt = dto.StartedAt,
            TargetDate = dto.TargetDate,
            Notes = dto.Notes,
            Links = Mapping.FromElement(dto.Links),
        };
        db.Projects.Add(p);
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.Id, includeArchived: true, ct);
    }

    public async Task<ProjectDto> ArchiveAsync(int id, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"project {id}");
        if (p.ArchivedAt is null)
        {
            p.ArchivedAt = DateTimeOffset.UtcNow;
            if (p.Status == ProjectStatus.InProgress || p.Status == ProjectStatus.Planned)
                p.Status = ProjectStatus.Abandoned;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.Id, includeArchived: true, ct);
    }

    public async Task<ProjectDto> UnarchiveAsync(int id, CancellationToken ct)
    {
        var p = await db.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"project {id}");
        p.ArchivedAt = null;
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.Id, includeArchived: true, ct);
    }

    public async Task HardDeleteAsync(int id, CancellationToken ct)
    {
        var p = await db.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"project {id}");
        db.Projects.Remove(p);
        await db.SaveChangesAsync(ct);
    }
}
