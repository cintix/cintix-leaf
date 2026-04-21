namespace Leaf.Web.Features.Shared.Models;

public sealed class Sprint
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string CapacityNote { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SprintState State { get; set; } = SprintState.Planned;
    public int PlannedStoryPoints { get; set; }
    public int CompletedStoryPoints { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ClosedUtc { get; set; }
}
