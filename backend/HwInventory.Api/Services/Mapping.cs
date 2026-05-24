using System.Text.Json;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;

namespace HwInventory.Api.Services;

/// <summary>
/// Hand-written entity → DTO mappers. We avoid AutoMapper/Mapperly in v1: surface
/// is small and the explicit code stays readable.
/// </summary>
public static class Mapping
{
    public static JsonElement? ToElement(JsonDocument? doc) =>
        doc?.RootElement.Clone();

    public static JsonDocument? FromElement(JsonElement? elem) =>
        elem is null ? null : JsonDocument.Parse(elem.Value.GetRawText());

    public static CategoryDto ToDto(Category c) =>
        new(c.Id, c.Name, c.Slug, c.ParentId, c.Icon, c.Description);

    public static TagDto ToDto(Tag t) =>
        new(t.Id, t.Name, t.Color);

    public static ActivityDto ToDto(Activity a) =>
        new(a.Id, a.HardwareId, a.ProjectId, a.Kind, a.Description,
            ToElement(a.Metadata), a.OccurredAt, a.CreatedAt);

    public static HardwareConfigDto ToDto(HardwareConfig c) =>
        new(c.Id, c.HardwareId, c.Kind, c.Name, c.Version, c.Notes,
            c.InstalledAt, c.IsCurrent, c.ActivityId, c.CreatedAt);

    public static LoanDto ToDto(Loan l) =>
        new(l.Id, l.HardwareId, l.LoanedTo, l.LoanedAt, l.DueAt, l.ReturnedAt, l.Notes, l.CreatedAt);

    public static HardwareSummaryDto ToSummary(Hardware h) =>
        new(h.Id, h.Name, h.Manufacturer, h.Model, h.Condition, h.Status, h.Location,
            h.LastUsedAt, h.LastActivityAt, h.ArchivedAt,
            h.Categories.Select(c => c.Name).ToList(),
            h.Tags.Select(t => t.Name).ToList(),
            h.CreatedAt, h.UpdatedAt);

    public static HardwareDto ToDto(Hardware h)
    {
        var projects = h.HardwareProjects
            .Select(hp => new HardwareProjectLinkDto(hp.ProjectId, hp.Project?.Title ?? "", hp.Role, hp.CreatedAt))
            .ToList();

        var currentConfigs = h.Configs.Where(c => c.IsCurrent).Select(ToDto).ToList();
        var activeLoan = h.Loans.FirstOrDefault(l => l.ReturnedAt == null);
        var recent = h.Activities.OrderByDescending(a => a.OccurredAt).Take(10).Select(ToDto).ToList();

        return new HardwareDto(
            h.Id, h.Name, h.Manufacturer, h.Model,
            h.SerialNumber, h.Sku, h.AssetTag, h.Revision,
            ToElement(h.Identifiers), ToElement(h.Specs), ToElement(h.Links),
            h.AcquiredAt, h.PurchasedFrom, h.PurchaseUrl, h.Cost, h.Currency,
            h.WarrantyExpiresAt, h.Location, h.Condition, h.Status, h.Notes,
            h.LastUsedAt, h.LastActivityAt, h.ArchivedAt,
            h.CreatedAt, h.UpdatedAt,
            h.Categories.Select(ToDto).ToList(),
            h.Tags.Select(ToDto).ToList(),
            projects, currentConfigs,
            activeLoan is null ? null : ToDto(activeLoan),
            recent);
    }

    public static ProjectSummaryDto ToSummary(Project p, int hardwareCount) =>
        new(p.Id, p.Title, p.Slug, p.Status, p.Priority, p.StartedAt, p.TargetDate,
            p.CompletedAt, p.ArchivedAt, hardwareCount, p.CreatedAt, p.UpdatedAt);

    public static ProjectDto ToDto(Project p) =>
        new(p.Id, p.Title, p.Slug, p.Description, p.Status, p.Priority,
            p.StartedAt, p.TargetDate, p.CompletedAt, p.Notes, ToElement(p.Links),
            p.ArchivedAt, p.CreatedAt, p.UpdatedAt,
            p.HardwareProjects.Select(hp =>
                new ProjectHardwareLinkDto(hp.HardwareId, hp.Hardware?.Name ?? "", hp.Role, hp.CreatedAt)
            ).ToList());

    /// <summary>
    /// Build a URL-safe slug from a free-text title. Falls back to a stable hash
    /// if the title contains no slug-safe characters at all.
    /// </summary>
    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Guid.NewGuid().ToString("N")[..8];
        var sb = new System.Text.StringBuilder(text.Length);
        var lastDash = true;
        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        var s = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(s) ? Guid.NewGuid().ToString("N")[..8] : s;
    }
}
