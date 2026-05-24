namespace HwInventory.Api.Models;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Color { get; set; }

    public ICollection<Hardware> Hardware { get; set; } = [];
}
