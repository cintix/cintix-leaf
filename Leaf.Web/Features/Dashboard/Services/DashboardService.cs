using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Dashboard.Services;

public sealed class DashboardService(ILeafRepository repository) : IDashboardService
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(int projectId, int currentUserId, CancellationToken ct = default)
    {
        var project = await repository.GetProjectAsync(projectId, ct);
        if (project is null)
        {
            return new DashboardSnapshot();
        }

        var activeSprint = await repository.GetActiveSprintAsync(projectId, ct);
        var source = activeSprint is not null
            ? await repository.GetBoardItemsAsync(projectId, activeSprint.Id, ct)
            : await repository.GetBacklogAsync(new WorkItemSearchFilters(projectId, null, null, null, null, null, null), ct);

        var counts = Enum.GetValues<WorkItemStatus>().ToDictionary(status => status, status => source.Count(x => x.Status == status));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new DashboardSnapshot
        {
            ProjectId = projectId,
            ProjectKey = project.Key,
            ProjectName = project.Name,
            ActiveSprint = activeSprint,
            CountsByStatus = counts,
            MyItems = source.Where(x => x.AssigneeUserId == currentUserId).OrderBy(x => x.Status).ThenBy(x => x.Title).Take(10).ToList(),
            OverdueItems = source.Where(x => x.DueDate.HasValue && x.DueDate.Value < today && x.Status != WorkItemStatus.Done).OrderBy(x => x.DueDate).Take(10).ToList(),
            RecentActivity = await repository.GetRecentActivityAsync(projectId, 12, ct)
        };
    }
}
