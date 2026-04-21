using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Search.Services;

public sealed class SearchService(ILeafRepository repository) : ISearchService
{
    public Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSearchFilters filters, CancellationToken ct = default)
        => repository.GetBacklogAsync(filters, ct);
}
