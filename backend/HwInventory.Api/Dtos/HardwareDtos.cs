using System.Text.Json;
using HwInventory.Api.Models;

namespace HwInventory.Api.Dtos;

public record HardwareSummaryDto(
    int Id,
    string Name,
    string? Manufacturer,
    string? Model,
    HardwareCondition Condition,
    HardwareStatus Status,
    string? Location,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? LastActivityAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record HardwareDto(
    int Id,
    string Name,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? Sku,
    string? AssetTag,
    string? Revision,
    JsonElement? Identifiers,
    JsonElement? Specs,
    JsonElement? Links,
    DateTimeOffset? AcquiredAt,
    string? PurchasedFrom,
    string? PurchaseUrl,
    decimal? Cost,
    string? Currency,
    DateTimeOffset? WarrantyExpiresAt,
    string? Location,
    HardwareCondition Condition,
    HardwareStatus Status,
    string? Notes,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? LastActivityAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<TagDto> Tags,
    IReadOnlyList<HardwareProjectLinkDto> Projects,
    IReadOnlyList<HardwareConfigDto> CurrentConfigs,
    LoanDto? ActiveLoan,
    IReadOnlyList<ActivityDto> RecentActivities);

public record HardwareCreateDto(
    string Name,
    string? Manufacturer = null,
    string? Model = null,
    string? SerialNumber = null,
    string? Sku = null,
    string? AssetTag = null,
    string? Revision = null,
    JsonElement? Identifiers = null,
    JsonElement? Specs = null,
    JsonElement? Links = null,
    DateTimeOffset? AcquiredAt = null,
    string? PurchasedFrom = null,
    string? PurchaseUrl = null,
    decimal? Cost = null,
    string? Currency = null,
    DateTimeOffset? WarrantyExpiresAt = null,
    string? Location = null,
    HardwareCondition Condition = HardwareCondition.Unknown,
    HardwareStatus Status = HardwareStatus.Available,
    string? Notes = null,
    IReadOnlyList<int>? CategoryIds = null,
    IReadOnlyList<int>? TagIds = null);

public record HardwareProjectLinkDto(int ProjectId, string ProjectTitle, string? Role, DateTimeOffset CreatedAt);

/// <summary>Filter inputs accepted by hardware list endpoints.</summary>
public record HardwareListFilter(
    int Limit = 50,
    int Offset = 0,
    string? Sort = null,
    string? Q = null,
    IReadOnlyList<int>? CategoryIds = null,
    IReadOnlyList<int>? TagIds = null,
    HardwareStatus? Status = null,
    HardwareCondition? Condition = null,
    int? IdleDays = null,
    bool IncludeArchived = false,
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null,
    DateTimeOffset? UpdatedAfter = null,
    DateTimeOffset? UpdatedBefore = null);
