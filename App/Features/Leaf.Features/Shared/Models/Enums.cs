namespace Leaf.Features.Shared.Models;

public enum WorkItemType
{
    Epic = 1,
    Story = 2,
    Task = 3,
    Bug = 4,
    Subtask = 5
}

public enum WorkItemStatus
{
    ToDo = 1,
    InProgress = 2,
    Review = 3,
    Done = 4
}

public enum WorkItemPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum SprintState
{
    Planned = 1,
    Active = 2,
    Closed = 3
}
