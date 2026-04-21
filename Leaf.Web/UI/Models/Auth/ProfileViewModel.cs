using Leaf.Web.Features.Shared.Models;
using Leaf.Web.UI.Models.Shell;

namespace Leaf.Web.UI.Models.Auth;

public sealed class ProfileViewModel
{
    public required ShellContextViewModel Shell { get; init; }
    public required User User { get; init; }
}
