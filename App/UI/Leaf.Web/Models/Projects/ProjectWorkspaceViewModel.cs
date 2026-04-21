using Leaf.Features.Shared.Models;
using Leaf.Web.Models.Shell;

namespace Leaf.Web.Models.Projects;

public sealed class ProjectWorkspaceViewModel
{
    public required ShellContextViewModel Shell { get; init; }
    public required Project Project { get; init; }
    public required IReadOnlyList<User> Members { get; init; }
    public required IReadOnlyList<User> Users { get; init; }
    public required IReadOnlyList<Sprint> Sprints { get; init; }
    public Sprint? ActiveSprint { get; init; }
    public required IReadOnlyList<WorkItem> Backlog { get; init; }
    public required IReadOnlyList<WorkItem> BoardItems { get; init; }
    public required IReadOnlyList<Label> Labels { get; init; }
    public required IReadOnlyList<ActivityEntry> Activity { get; init; }

    public string Query { get; init; } = string.Empty;
    public int? AssigneeFilter { get; init; }
    public int? SprintFilter { get; init; }
    public string LabelFilter { get; init; } = string.Empty;
}
