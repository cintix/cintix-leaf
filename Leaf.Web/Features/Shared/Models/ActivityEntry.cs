namespace Leaf.Web.Features.Shared.Models;

public sealed class ActivityEntry
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? WorkItemId { get; set; }
    public int ActorUserId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
