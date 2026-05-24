using System.Text.Json;
using HwInventory.Api.Models;

namespace HwInventory.Api.Dtos;

public record ProjectDto(
    int Id,
    string Title,
    string Slug,
    string? Description,
    ProjectStatus Status,
    ProjectPriority Priority,
    DateTimeOffset? StartedAt,
    DateTimeOffset? TargetDate,
    DateTimeOffset? CompletedAt,
    string? Notes,
    JsonElement? Links,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ProjectHardwareLinkDto> Hardware);

public record ProjectSummaryDto(
    int Id,
    string Title,
    string Slug,
    ProjectStatus Status,
    ProjectPriority Priority,
    DateTimeOffset? StartedAt,
    DateTimeOffset? TargetDate,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ArchivedAt,
    int HardwareCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ProjectCreateDto(
    string Title,
    string? Slug = null,
    string? Description = null,
    ProjectStatus Status = ProjectStatus.Idea,
    ProjectPriority Priority = ProjectPriority.Medium,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? TargetDate = null,
    string? Notes = null,
    JsonElement? Links = null);

public record ProjectHardwareLinkDto(int HardwareId, string HardwareName, string? Role, DateTimeOffset CreatedAt);

public record ProjectListFilter(
    int Limit = 50,
    int Offset = 0,
    string? Sort = null,
    string? Q = null,
    ProjectStatus? Status = null,
    ProjectPriority? Priority = null,
    bool IncludeArchived = false,
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null);
