namespace Leaf.Web.Features.Shared.Models;

public sealed class DashboardSnapshot
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Sprint? ActiveSprint { get; set; }
    public IReadOnlyDictionary<WorkItemStatus, int> CountsByStatus { get; set; } = new Dictionary<WorkItemStatus, int>();
    public IReadOnlyList<WorkItem> MyItems { get; set; } = [];
    public IReadOnlyList<WorkItem> OverdueItems { get; set; } = [];
    public IReadOnlyList<ActivityEntry> RecentActivity { get; set; } = [];
}
