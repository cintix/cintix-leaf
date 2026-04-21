using Leaf.Features.Shared.Contracts;
using Leaf.Features.Shared.Core;
using Leaf.Features.Shared.Models;

namespace Leaf.Features.Search.Core;

public sealed class SearchService(ILeafRepository repository)
{
    public Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSearchFilters filters, CancellationToken ct = default)
        => repository.GetBacklogAsync(filters, ct);
}
