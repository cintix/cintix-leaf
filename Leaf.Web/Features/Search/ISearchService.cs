using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Search;

public interface ISearchService
{
    Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSearchFilters filters, CancellationToken ct = default);
}
