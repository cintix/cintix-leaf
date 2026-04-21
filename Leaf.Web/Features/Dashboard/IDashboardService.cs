using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(int projectId, int currentUserId, CancellationToken ct = default);
}
