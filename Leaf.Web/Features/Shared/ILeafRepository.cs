using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Shared;

public interface ILeafRepository
{
    Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetUsersAsync(bool includeInactive, CancellationToken ct = default);
    Task<int> CreateUserAsync(User user, CancellationToken ct = default);
    Task UpdateUserAsync(UpdateUserInput input, CancellationToken ct = default);

    Task<IReadOnlyList<Project>> GetProjectsForUserAsync(int userId, bool includeArchived, CancellationToken ct = default);
    Task<Project?> GetProjectAsync(int projectId, CancellationToken ct = default);
    Task<int> CreateProjectAsync(CreateProjectInput input, CancellationToken ct = default);
    Task UpdateProjectAsync(UpdateProjectInput input, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetProjectMembersAsync(int projectId, CancellationToken ct = default);
    Task ReplaceProjectMembersAsync(int projectId, IReadOnlyCollection<int> memberUserIds, CancellationToken ct = default);

    Task<IReadOnlyList<WorkItem>> GetBacklogAsync(WorkItemSearchFilters filters, CancellationToken ct = default);
    Task<IReadOnlyList<WorkItem>> GetBoardItemsAsync(int projectId, int? sprintId, CancellationToken ct = default);
    Task<WorkItem?> GetWorkItemAsync(int id, CancellationToken ct = default);
    Task<int> CreateWorkItemAsync(WorkItem item, CancellationToken ct = default);
    Task UpdateWorkItemAsync(UpdateWorkItemInput input, CancellationToken ct = default);
    Task MoveWorkItemAsync(int itemId, WorkItemStatus status, int columnOrder, int? sprintId, CancellationToken ct = default);
    Task ReorderBacklogAsync(int projectId, IReadOnlyList<int> orderedWorkItemIds, CancellationToken ct = default);
    Task MoveWorkItemsToSprintAsync(int projectId, int sprintId, IReadOnlyCollection<int> itemIds, CancellationToken ct = default);

    Task<IReadOnlyList<Sprint>> GetSprintsAsync(int projectId, CancellationToken ct = default);
    Task<Sprint?> GetSprintAsync(int sprintId, CancellationToken ct = default);
    Task<Sprint?> GetActiveSprintAsync(int projectId, CancellationToken ct = default);
    Task<int> CreateSprintAsync(Sprint sprint, IReadOnlyCollection<int> workItemIds, CancellationToken ct = default);
    Task StartSprintAsync(int sprintId, CancellationToken ct = default);
    Task CloseSprintAsync(int sprintId, bool moveUnfinishedToBacklog, int? nextSprintId, CancellationToken ct = default);

    Task<int> AddCommentAsync(Comment comment, CancellationToken ct = default);
    Task<IReadOnlyList<Comment>> GetCommentsAsync(int workItemId, CancellationToken ct = default);

    Task<IReadOnlyList<Label>> GetLabelsAsync(int projectId, CancellationToken ct = default);
    Task<int> CreateLabelAsync(Label label, CancellationToken ct = default);

    Task AddActivityAsync(ActivityEntry activity, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityEntry>> GetRecentActivityAsync(int projectId, int take, CancellationToken ct = default);

    Task<IReadOnlyList<(DateOnly Day, int Remaining)>> GetBurndownAsync(int sprintId, CancellationToken ct = default);
    Task<IReadOnlyList<VelocityPoint>> GetVelocityAsync(int projectId, CancellationToken ct = default);
    Task<ThroughputSummary> GetThroughputSummaryAsync(int projectId, CancellationToken ct = default);

    Task SeedDemoDataIfEmptyAsync(CancellationToken ct = default);
}
