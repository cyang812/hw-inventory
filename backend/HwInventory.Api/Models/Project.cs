using System.Text.Json;

namespace HwInventory.Api.Models;

public class Project
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Idea;
    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? TargetDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public JsonDocument? Links { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<HardwareProject> HardwareProjects { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
}
