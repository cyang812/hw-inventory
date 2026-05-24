using System.Text.Json;

namespace HwInventory.Api.Models;

public class Hardware
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    public string? SerialNumber { get; set; }
    public string? Sku { get; set; }
    public string? AssetTag { get; set; }
    public string? Revision { get; set; }

    public JsonDocument? Identifiers { get; set; }
    public JsonDocument? Specs { get; set; }
    public JsonDocument? Links { get; set; }

    public DateTimeOffset? AcquiredAt { get; set; }
    public string? PurchasedFrom { get; set; }
    public string? PurchaseUrl { get; set; }
    public decimal? Cost { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset? WarrantyExpiresAt { get; set; }

    public string? Location { get; set; }

    public HardwareCondition Condition { get; set; } = HardwareCondition.Unknown;
    public HardwareStatus Status { get; set; } = HardwareStatus.Available;

    public string? Notes { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
    public ICollection<HardwareProject> HardwareProjects { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<HardwareConfig> Configs { get; set; } = [];
    public ICollection<Loan> Loans { get; set; } = [];
}
