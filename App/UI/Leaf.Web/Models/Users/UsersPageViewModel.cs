using Leaf.Features.Shared.Models;
using Leaf.Web.Models.Shell;

namespace Leaf.Web.Models.Users;

public sealed class UsersPageViewModel
{
    public required ShellContextViewModel Shell { get; init; }
    public required IReadOnlyList<User> Users { get; init; }
    public required Dictionary<int, int> WorkloadByUserId { get; init; }
    public int? ProjectId { get; init; }
}
