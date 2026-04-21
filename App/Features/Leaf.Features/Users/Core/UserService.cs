using Leaf.Core.Primitives;
using Leaf.Features.Shared.Contracts;
using Leaf.Features.Shared.Core;
using Leaf.Features.Shared.Models;

namespace Leaf.Features.Users.Core;

public sealed class UserService(ILeafRepository repository)
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
