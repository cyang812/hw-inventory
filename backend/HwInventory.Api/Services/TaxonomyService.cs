using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Services;

public class CategoryService(AppDbContext db)
{
    public async Task<ListResponse<CategoryDto>> ListAsync(string? q, int limit, int offset, CancellationToken ct)
    {
        var query = db.Categories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Name, like) || EF.Functions.Like(c.Slug, like));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip(offset).Take(limit)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.Icon, c.Description))
            .ToListAsync(ct);
        return new ListResponse<CategoryDto>(items, total, limit, offset);
    }

    public async Task<CategoryDto> GetAsync(int id, CancellationToken ct)
    {
        var c = await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"category {id}");
        return Mapping.ToDto(c);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("name is required");
        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? Mapping.Slugify(dto.Name) : dto.Slug.Trim();
        if (await db.Categories.AnyAsync(c => c.Slug == slug, ct))
            throw new ConflictException($"category with slug '{slug}' already exists");

        var entity = new Category
        {
            Name = dto.Name.Trim(),
            Slug = slug,
            ParentId = dto.ParentId,
            Icon = dto.Icon,
            Description = dto.Description,
        };
        db.Categories.Add(entity);
        await db.SaveChangesAsync(ct);
        return Mapping.ToDto(entity);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryUpdateDto dto, CancellationToken ct)
    {
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"category {id}");

        if (dto.Name is not null) c.Name = dto.Name;
        if (dto.Slug is not null)
        {
            if (await db.Categories.AnyAsync(x => x.Slug == dto.Slug && x.Id != id, ct))
                throw new ConflictException($"category with slug '{dto.Slug}' already exists");
            c.Slug = dto.Slug;
        }
        if (dto.ParentId is not null) c.ParentId = dto.ParentId;
        if (dto.Icon is not null) c.Icon = dto.Icon;
        if (dto.Description is not null) c.Description = dto.Description;

        await db.SaveChangesAsync(ct);
        return Mapping.ToDto(c);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"category {id}");
        db.Categories.Remove(c);
        await db.SaveChangesAsync(ct);
    }
}

public class TagService(AppDbContext db)
{
    public async Task<ListResponse<TagDto>> ListAsync(string? q, int limit, int offset, CancellationToken ct)
    {
        var query = db.Tags.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(t => EF.Functions.Like(t.Name, like));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.Name)
            .Skip(offset).Take(limit)
            .Select(t => new TagDto(t.Id, t.Name, t.Color))
            .ToListAsync(ct);
        return new ListResponse<TagDto>(items, total, limit, offset);
    }

    public async Task<TagDto> CreateAsync(TagCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("name is required");
        var name = dto.Name.Trim();
        var existing = await db.Tags.FirstOrDefaultAsync(t => t.Name == name, ct);
        if (existing != null) return Mapping.ToDto(existing); // idempotent
        var t = new Tag { Name = name, Color = dto.Color };
        db.Tags.Add(t);
        await db.SaveChangesAsync(ct);
        return Mapping.ToDto(t);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var t = await db.Tags.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"tag {id}");
        db.Tags.Remove(t);
        await db.SaveChangesAsync(ct);
    }
}
