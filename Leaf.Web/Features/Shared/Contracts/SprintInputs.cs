namespace Leaf.Web.Features.Shared.Contracts;

public sealed record CreateSprintInput(
    int ProjectId,
    string Name,
    string Goal,
    string CapacityNote,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyCollection<int> WorkItemIds);

public sealed record CloseSprintInput(int SprintId, bool MoveUnfinishedToBacklog, int? NextSprintId);
