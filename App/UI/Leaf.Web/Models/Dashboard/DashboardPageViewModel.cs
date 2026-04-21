using Leaf.Features.Shared.Models;
using Leaf.Web.Models.Shell;

namespace Leaf.Web.Models.Dashboard;

public sealed class DashboardPageViewModel
{
    public ShellContextViewModel Shell { get; set; } = new();
    public DashboardSnapshot Snapshot { get; set; } = new();
}
