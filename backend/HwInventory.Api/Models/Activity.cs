using System.Text.Json;

namespace HwInventory.Api.Models;

public class Activity
{
    public int Id { get; set; }

    public int HardwareId { get; set; }
    public Hardware Hardware { get; set; } = null!;

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public ActivityKind Kind { get; set; }
    public string? Description { get; set; }
    public JsonDocument? Metadata { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
