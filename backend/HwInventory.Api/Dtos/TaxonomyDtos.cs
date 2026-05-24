namespace HwInventory.Api.Dtos;

public record CategoryDto(int Id, string Name, string Slug, int? ParentId, string? Icon, string? Description);

public record CategoryCreateDto(string Name, string Slug, int? ParentId = null, string? Icon = null, string? Description = null);

public record CategoryUpdateDto(string? Name = null, string? Slug = null, int? ParentId = null, string? Icon = null, string? Description = null);

public record TagDto(int Id, string Name, string? Color);

public record TagCreateDto(string Name, string? Color = null);
