using Leaf.Web.Features.Shared.Models;
using Leaf.Web.UI.Models.Shell;

namespace Leaf.Web.UI.Models.Users;

public sealed class UsersPageViewModel
{
    public required ShellContextViewModel Shell { get; init; }
    public required IReadOnlyList<User> Users { get; init; }
    public required Dictionary<int, int> WorkloadByUserId { get; init; }
    public int? ProjectId { get; init; }
}
