using Leaf.Features.Shared.Models;
using Leaf.Web.Models.Shell;

namespace Leaf.Web.Models.Auth;

public sealed class ProfileViewModel
{
    public required ShellContextViewModel Shell { get; init; }
    public required User User { get; init; }
}
