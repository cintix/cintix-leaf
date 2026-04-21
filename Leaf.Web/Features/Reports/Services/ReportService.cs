using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Reports.Services;

public sealed class ReportService(ILeafRepository repository) : IReportService
{
    public async Task<IReadOnlyList<BurndownPoint>> GetBurndownAsync(int sprintId, CancellationToken ct = default)
    {
        var points = await repository.GetBurndownAsync(sprintId, ct);
        return points.Select(x => new BurndownPoint(x.Day, x.Remaining)).ToList();
    }

    public Task<IReadOnlyList<VelocityPoint>> GetVelocityAsync(int projectId, CancellationToken ct = default)
        => repository.GetVelocityAsync(projectId, ct);

    public Task<ThroughputSummary> GetThroughputSummaryAsync(int projectId, CancellationToken ct = default)
        => repository.GetThroughputSummaryAsync(projectId, ct);
}
