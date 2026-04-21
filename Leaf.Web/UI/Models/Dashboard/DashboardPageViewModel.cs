using Leaf.Web.Features.Shared.Models;
using Leaf.Web.UI.Models.Shell;

namespace Leaf.Web.UI.Models.Dashboard;

public sealed class DashboardPageViewModel
{
    public ShellContextViewModel Shell { get; set; } = new();
    public DashboardSnapshot Snapshot { get; set; } = new();
}
