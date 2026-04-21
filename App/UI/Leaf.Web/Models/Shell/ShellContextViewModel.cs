using Leaf.Features.Shared.Models;

namespace Leaf.Web.Models.Shell;

public sealed class ShellContextViewModel
{
    public User? CurrentUser { get; set; }
    public IReadOnlyList<Project> Projects { get; set; } = [];
    public int? CurrentProjectId { get; set; }
    public string CurrentArea { get; set; } = "Dashboard";
}
