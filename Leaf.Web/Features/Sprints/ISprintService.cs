using Leaf.Web.Core.Primitives;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Sprints;

public interface ISprintService
{
    Task<IReadOnlyList<Sprint>> GetSprintsAsync(int projectId, CancellationToken ct = default);
    Task<Sprint?> GetActiveSprintAsync(int projectId, CancellationToken ct = default);
    Task<Result<int>> CreateSprintAsync(CreateSprintInput input, CancellationToken ct = default);
    Task<Result> StartSprintAsync(int sprintId, int actorUserId, CancellationToken ct = default);
    Task<Result> CloseSprintAsync(CloseSprintInput input, int actorUserId, CancellationToken ct = default);
    Task<(int planned, int completed)> GetStoryPointTotalsAsync(int sprintId, CancellationToken ct = default);
}
