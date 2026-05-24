namespace HwInventory.Api.Models;

/// <summary>
/// Explicit join entity carrying <see cref="Role"/> and timestamp.
/// </summary>
public class HardwareProject
{
    public int HardwareId { get; set; }
    public Hardware Hardware { get; set; } = null!;

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string? Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
