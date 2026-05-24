using System.Text.Json;
using HwInventory.Api.Models;

namespace HwInventory.Api.Dtos;

public record ActivityDto(
    int Id,
    int HardwareId,
    int? ProjectId,
    ActivityKind Kind,
    string? Description,
    JsonElement? Metadata,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt);

public record ActivityCreateDto(
    ActivityKind Kind,
    string? Description = null,
    JsonElement? Metadata = null,
    DateTimeOffset? OccurredAt = null,
    int? ProjectId = null);

public record ActivityListFilter(
    int Limit = 50,
    int Offset = 0,
    string? Sort = "-occurredAt",
    int? HardwareId = null,
    int? ProjectId = null,
    ActivityKind? Kind = null,
    DateTimeOffset? OccurredAfter = null,
    DateTimeOffset? OccurredBefore = null);

public record HardwareConfigDto(
    int Id,
    int HardwareId,
    HardwareConfigKind Kind,
    string Name,
    string? Version,
    string? Notes,
    DateTimeOffset? InstalledAt,
    bool IsCurrent,
    int? ActivityId,
    DateTimeOffset CreatedAt);

public record HardwareConfigCreateDto(
    HardwareConfigKind Kind,
    string Name,
    string? Version = null,
    string? Notes = null,
    DateTimeOffset? InstalledAt = null,
    bool IsCurrent = true,
    bool LogActivity = true);

public record HardwareConfigListFilter(HardwareConfigKind? Kind = null, bool? Current = null, int Limit = 50, int Offset = 0);

public record LoanDto(
    int Id,
    int HardwareId,
    string LoanedTo,
    DateTimeOffset LoanedAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? ReturnedAt,
    string? Notes,
    DateTimeOffset CreatedAt);

public record LoanCreateDto(string LoanedTo, DateTimeOffset? LoanedAt = null, DateTimeOffset? DueAt = null, string? Notes = null);

public record LoanUpdateDto(DateTimeOffset? DueAt = null, DateTimeOffset? ReturnedAt = null, string? Notes = null, bool ClearReturned = false);

public record DashboardStatsDto(
    int HardwareTotal,
    int HardwareArchived,
    int ProjectsTotal,
    int ActivitiesLast30Days,
    int IdleCount,
    int OverdueLoans,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByCondition,
    IReadOnlyDictionary<string, int> ByCategory,
    IReadOnlyDictionary<string, int> ProjectsByStatus);
