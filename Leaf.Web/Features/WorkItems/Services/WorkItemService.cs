using Leaf.Web.Core.Primitives;
using Leaf.Web.Core.Time;
using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.WorkItems.Services;

public sealed class WorkItemService(ILeafRepository repository, IClock clock) : IWorkItemService
{
    public Task<IReadOnlyList<WorkItem>> GetBacklogAsync(WorkItemSearchFilters filters, CancellationToken ct = default)
        => repository.GetBacklogAsync(filters, ct);

    public Task<IReadOnlyList<WorkItem>> GetBoardItemsAsync(int projectId, int? sprintId, CancellationToken ct = default)
        => repository.GetBoardItemsAsync(projectId, sprintId, ct);

    public Task<WorkItem?> GetByIdAsync(int id, CancellationToken ct = default)
        => repository.GetWorkItemAsync(id, ct);

    public async Task<Result<int>> CreateAsync(CreateWorkItemInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Result<int>.Failure(Error.Validation("Title is required."));
        }

        var existing = await repository.GetBacklogAsync(new WorkItemSearchFilters(
            input.ProjectId,
            null,
            null,
            null,
            null,
            null,
            null), ct);

        var maxRank = existing.Count == 0 ? 0 : existing.Max(x => x.BacklogRank);

        var workItem = new WorkItem
        {
            ProjectId = input.ProjectId,
            SprintId = input.SprintId,
            ParentId = input.ParentId,
            Title = input.Title.Trim(),
            Description = input.Description.Trim(),
            Type = input.Type,
            Status = WorkItemStatus.ToDo,
            Priority = input.Priority,
            AssigneeUserId = input.AssigneeUserId,
            ReporterUserId = input.ReporterUserId,
            LabelsCsv = string.Join(',', input.Labels.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))),
            StoryPoints = Math.Max(0, input.StoryPoints),
            DueDate = input.DueDate,
            BacklogRank = maxRank + 1,
            ColumnOrder = 0,
            CreatedUtc = clock.UtcNow,
            UpdatedUtc = clock.UtcNow
        };

        var itemId = await repository.CreateWorkItemAsync(workItem, ct);
        await repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = input.ProjectId,
            WorkItemId = itemId,
            ActorUserId = input.ReporterUserId,
            Kind = "work_item_created",
            Description = $"Created {input.Type}: {workItem.Title}",
            CreatedUtc = clock.UtcNow
        }, ct);

        return Result<int>.Success(itemId);
    }

    public async Task<Result> UpdateAsync(UpdateWorkItemInput input, int actorUserId, CancellationToken ct = default)
    {
        if (input.Id <= 0 || string.IsNullOrWhiteSpace(input.Title))
        {
            return Result.Failure(Error.Validation("Invalid work item payload."));
        }

        var existing = await repository.GetWorkItemAsync(input.Id, ct);
        if (existing is null)
        {
            return Result.Failure(Error.NotFound("Work item not found."));
        }

        await repository.UpdateWorkItemAsync(input with
        {
            Title = input.Title.Trim(),
            Description = input.Description.Trim(),
            Labels = input.Labels.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            StoryPoints = Math.Max(0, input.StoryPoints)
        }, ct);

        if (existing.Status != input.Status)
        {
            await repository.AddActivityAsync(new ActivityEntry
            {
                ProjectId = existing.ProjectId,
                WorkItemId = input.Id,
                ActorUserId = actorUserId,
                Kind = "status_changed",
                Description = $"Status changed from {existing.Status} to {input.Status}",
                CreatedUtc = clock.UtcNow
            }, ct);
        }

        return Result.Success();
    }

    public async Task<Result> MoveAsync(int itemId, WorkItemStatus status, int columnOrder, int? sprintId, int actorUserId, CancellationToken ct = default)
    {
        var existing = await repository.GetWorkItemAsync(itemId, ct);
        if (existing is null)
        {
            return Result.Failure(Error.NotFound("Work item not found."));
        }

        await repository.MoveWorkItemAsync(itemId, status, columnOrder, sprintId, ct);

        await repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = existing.ProjectId,
            WorkItemId = existing.Id,
            ActorUserId = actorUserId,
            Kind = "board_moved",
            Description = $"Moved to {status}",
            CreatedUtc = clock.UtcNow
        }, ct);

        return Result.Success();
    }

    public Task ReorderBacklogAsync(int projectId, IReadOnlyList<int> orderedIds, CancellationToken ct = default)
        => repository.ReorderBacklogAsync(projectId, orderedIds, ct);

    public Task MoveToSprintAsync(int projectId, int sprintId, IReadOnlyCollection<int> itemIds, CancellationToken ct = default)
        => repository.MoveWorkItemsToSprintAsync(projectId, sprintId, itemIds, ct);

    public Task<IReadOnlyList<Comment>> GetCommentsAsync(int workItemId, CancellationToken ct = default)
        => repository.GetCommentsAsync(workItemId, ct);

    public Task<int> AddCommentAsync(Comment comment, CancellationToken ct = default)
        => repository.AddCommentAsync(comment, ct);
}
