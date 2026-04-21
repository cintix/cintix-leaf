using Leaf.Features.Shared.Core;
using Leaf.Features.Shared.Models;

namespace Leaf.Features.Reports.Core;

public sealed class ReportService(ILeafRepository repository)
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
