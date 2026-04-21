using Leaf.Web.Core.Primitives;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<Project>> GetProjectsForUserAsync(int userId, bool includeArchived = false, CancellationToken ct = default);
    Task<Project?> GetProjectAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetMembersAsync(int projectId, CancellationToken ct = default);
    Task<Result<int>> CreateProjectAsync(CreateProjectInput input, CancellationToken ct = default);
    Task<Result> UpdateProjectAsync(UpdateProjectInput input, CancellationToken ct = default);
    Task SetMembersAsync(int projectId, IReadOnlyCollection<int> memberUserIds, CancellationToken ct = default);
}
