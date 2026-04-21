using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Reports;

public interface IReportService
{
    Task<IReadOnlyList<BurndownPoint>> GetBurndownAsync(int sprintId, CancellationToken ct = default);
    Task<IReadOnlyList<VelocityPoint>> GetVelocityAsync(int projectId, CancellationToken ct = default);
    Task<ThroughputSummary> GetThroughputSummaryAsync(int projectId, CancellationToken ct = default);
}
