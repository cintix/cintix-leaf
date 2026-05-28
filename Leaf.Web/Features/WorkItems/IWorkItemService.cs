using Leaf.Web.Core.Primitives;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.WorkItems;

public interface IWorkItemService
{
    Task<IReadOnlyList<WorkItem>> GetBacklogAsync(WorkItemSearchFilters filters, CancellationToken ct = default);
    Task<IReadOnlyList<WorkItem>> GetBoardItemsAsync(int projectId, int? sprintId, CancellationToken ct = default);
    Task<WorkItem?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(CreateWorkItemInput input, CancellationToken ct = default);
    Task<Result> UpdateAsync(UpdateWorkItemInput input, int actorUserId, CancellationToken ct = default);
    Task<Result> MoveAsync(int itemId, WorkItemStatus status, int columnOrder, int? sprintId, int actorUserId, CancellationToken ct = default);
    Task ReorderBacklogAsync(int projectId, IReadOnlyList<int> orderedIds, CancellationToken ct = default);
    Task MoveToSprintAsync(int projectId, int sprintId, IReadOnlyCollection<int> itemIds, CancellationToken ct = default);
    Task<IReadOnlyList<Comment>> GetCommentsAsync(int workItemId, CancellationToken ct = default);
    Task<int> AddCommentAsync(Comment comment, CancellationToken ct = default);
    Task<WorkItemDetailDto?> GetDetailAsync(int id, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, int actorUserId, CancellationToken ct = default);
}
