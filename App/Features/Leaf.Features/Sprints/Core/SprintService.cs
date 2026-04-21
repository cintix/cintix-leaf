using Leaf.Core.Primitives;
using Leaf.Core.Time;
using Leaf.Features.Shared.Contracts;
using Leaf.Features.Shared.Core;
using Leaf.Features.Shared.Models;

namespace Leaf.Features.Sprints.Core;

public sealed class SprintService(ILeafRepository repository, IClock clock)
{
    public Task<IReadOnlyList<Sprint>> GetSprintsAsync(int projectId, CancellationToken ct = default)
        => repository.GetSprintsAsync(projectId, ct);

    public Task<Sprint?> GetActiveSprintAsync(int projectId, CancellationToken ct = default)
        => repository.GetActiveSprintAsync(projectId, ct);

    public async Task<Result<int>> CreateSprintAsync(CreateSprintInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return Result<int>.Failure(Error.Validation("Sprint name is required."));
        }

        if (input.EndDate < input.StartDate)
        {
            return Result<int>.Failure(Error.Validation("Sprint end date must be after start date."));
        }

        var sprint = new Sprint
        {
            ProjectId = input.ProjectId,
            Name = input.Name.Trim(),
            Goal = input.Goal.Trim(),
            CapacityNote = input.CapacityNote.Trim(),
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            State = SprintState.Planned,
            CreatedUtc = clock.UtcNow
        };

        var sprintId = await repository.CreateSprintAsync(sprint, input.WorkItemIds, ct);

        await repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = input.ProjectId,
            ActorUserId = 0,
            Kind = "sprint_created",
            Description = $"Sprint created: {sprint.Name}",
            CreatedUtc = clock.UtcNow
        }, ct);

        return Result<int>.Success(sprintId);
    }

    public async Task<Result> StartSprintAsync(int sprintId, int actorUserId, CancellationToken ct = default)
    {
        var sprint = await repository.GetSprintAsync(sprintId, ct);
        if (sprint is null)
        {
            return Result.Failure(Error.NotFound("Sprint not found."));
        }

        await repository.StartSprintAsync(sprintId, ct);

        await repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = sprint.ProjectId,
            ActorUserId = actorUserId,
            Kind = "sprint_started",
            Description = $"Started sprint {sprint.Name}",
            CreatedUtc = clock.UtcNow
        }, ct);

        return Result.Success();
    }

    public async Task<Result> CloseSprintAsync(CloseSprintInput input, int actorUserId, CancellationToken ct = default)
    {
        var sprint = await repository.GetSprintAsync(input.SprintId, ct);
        if (sprint is null)
        {
            return Result.Failure(Error.NotFound("Sprint not found."));
        }

        await repository.CloseSprintAsync(input.SprintId, input.MoveUnfinishedToBacklog, input.NextSprintId, ct);

        await repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = sprint.ProjectId,
            ActorUserId = actorUserId,
            Kind = "sprint_closed",
            Description = $"Closed sprint {sprint.Name}",
            CreatedUtc = clock.UtcNow
        }, ct);

        return Result.Success();
    }

    public async Task<(int planned, int completed)> GetStoryPointTotalsAsync(int sprintId, CancellationToken ct = default)
    {
        var sprint = await repository.GetSprintAsync(sprintId, ct);
        return sprint is null ? (0, 0) : (sprint.PlannedStoryPoints, sprint.CompletedStoryPoints);
    }
}
