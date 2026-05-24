namespace HwInventory.Api.Models;

public class HardwareConfig
{
    public int Id { get; set; }

    public int HardwareId { get; set; }
    public Hardware Hardware { get; set; } = null!;

    public HardwareConfigKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? InstalledAt { get; set; }
    public bool IsCurrent { get; set; }

    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
