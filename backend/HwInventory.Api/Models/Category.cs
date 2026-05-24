namespace HwInventory.Api.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public string? Icon { get; set; }
    public string? Description { get; set; }

    public ICollection<Hardware> Hardware { get; set; } = [];
}
