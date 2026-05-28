namespace Leaf.Web.Features.Shared.Models;

public sealed class WorkItem
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public int? SprintId { get; set; }
    public int? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkItemType Type { get; set; } = WorkItemType.Story;
    public WorkItemStatus Status { get; set; } = WorkItemStatus.ToDo;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public int? AssigneeUserId { get; set; }
    public int ReporterUserId { get; set; }
    public string LabelsCsv { get; set; } = string.Empty;
    public int StoryPoints { get; set; }
    public DateOnly? DueDate { get; set; }
    public int BacklogRank { get; set; }
    public int ColumnOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
