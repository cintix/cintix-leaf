using Leaf.Features.Shared.Models;

namespace Leaf.Features.Shared.Contracts;

public sealed record CreateWorkItemInput(
    int ProjectId,
    int? SprintId,
    int? ParentId,
    string Title,
    string Description,
    WorkItemType Type,
    WorkItemPriority Priority,
    int? AssigneeUserId,
    int ReporterUserId,
    IReadOnlyCollection<string> Labels,
    int StoryPoints,
    DateOnly? DueDate);

public sealed record UpdateWorkItemInput(
    int Id,
    string Title,
    string Description,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    int? AssigneeUserId,
    IReadOnlyCollection<string> Labels,
    int StoryPoints,
    DateOnly? DueDate);

public sealed record WorkItemSearchFilters(
    int ProjectId,
    string? Query,
    int? AssigneeUserId,
    int? SprintId,
    WorkItemStatus? Status,
    WorkItemType? Type,
    IReadOnlyCollection<string>? Labels);
