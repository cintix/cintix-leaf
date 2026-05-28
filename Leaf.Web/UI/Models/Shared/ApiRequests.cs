using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.UI.Models.Shared;

public sealed class CreateWorkItemRequest
{
    public int ProjectId { get; set; }
    public int? SprintId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkItemType Type { get; set; } = WorkItemType.Story;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public int? AssigneeUserId { get; set; }
    public int StoryPoints { get; set; }
    public string Labels { get; set; } = string.Empty;
}

public sealed class ReorderBacklogRequest
{
    public int ProjectId { get; set; }
    public List<int> OrderedIds { get; set; } = [];
}

public sealed class MoveWorkItemRequest
{
    public int WorkItemId { get; set; }
    public WorkItemStatus Status { get; set; }
    public int ColumnOrder { get; set; }
    public int? SprintId { get; set; }
}

public sealed class CreateSprintRequest
{
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string CapacityNote { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<int> WorkItemIds { get; set; } = [];
}

public sealed class CloseSprintRequest
{
    public bool MoveUnfinishedToBacklog { get; set; } = true;
    public int? NextSprintId { get; set; }
}

public sealed class MoveToSprintRequest
{
    public int ProjectId { get; set; }
    public int SprintId { get; set; }
    public List<int> ItemIds { get; set; } = [];
}

public sealed class AddCommentRequest
{
    public int WorkItemId { get; set; }
    public string Body { get; set; } = string.Empty;
}

public sealed class UpdateWorkItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkItemType Type { get; set; }
    public WorkItemStatus Status { get; set; }
    public WorkItemPriority Priority { get; set; }
    public int? AssigneeUserId { get; set; }
    public int StoryPoints { get; set; }
    public string Labels { get; set; } = string.Empty;
    public string? DueDate { get; set; }
}

public sealed class CreateLabelRequest
{
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#6BAA75";
}
