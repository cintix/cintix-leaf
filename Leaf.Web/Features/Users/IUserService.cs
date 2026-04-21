using Leaf.Web.Core.Primitives;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Users;

public interface IUserService
{
    Task<IReadOnlyList<User>> GetUsersAsync(bool includeInactive, CancellationToken ct = default);
    Task<Result> UpdateUserAsync(UpdateUserInput input, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetWorkloadSummaryAsync(int projectId, CancellationToken ct = default);
}
