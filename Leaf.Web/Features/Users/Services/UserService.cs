using Leaf.Web.Core.Primitives;
using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Users.Services;

public sealed class UserService(ILeafRepository repository) : IUserService
{
    public Task<IReadOnlyList<User>> GetUsersAsync(bool includeInactive, CancellationToken ct = default)
        => repository.GetUsersAsync(includeInactive, ct);

    public async Task<Result> UpdateUserAsync(UpdateUserInput input, CancellationToken ct = default)
    {
        if (input.Id <= 0)
        {
            return Result.Failure(Error.Validation("Invalid user id."));
        }

        if (string.IsNullOrWhiteSpace(input.DisplayName) || string.IsNullOrWhiteSpace(input.Email))
        {
            return Result.Failure(Error.Validation("Display name and email are required."));
        }

        await repository.UpdateUserAsync(input with
        {
            DisplayName = input.DisplayName.Trim(),
            Email = input.Email.Trim()
        }, ct);

        return Result.Success();
    }

    public async Task<Dictionary<int, int>> GetWorkloadSummaryAsync(int projectId, CancellationToken ct = default)
    {
        var items = await repository.GetBoardItemsAsync(projectId, null, ct);
        return items
            .Where(x => x.AssigneeUserId.HasValue)
            .GroupBy(x => x.AssigneeUserId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
