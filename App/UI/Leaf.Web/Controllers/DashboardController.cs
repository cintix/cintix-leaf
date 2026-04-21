using Leaf.Features.Dashboard.Core;
using Leaf.Features.Projects.Core;
using Leaf.Features.Shared.Core;
using Leaf.Web.Models.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Leaf.Web.Controllers;

public sealed class DashboardController : LeafControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly ProjectService _projectService;

    public DashboardController(
        DashboardService dashboardService,
        ILeafRepository repository,
        ProjectService projectService)
        : base(repository, projectService)
    {
        _dashboardService = dashboardService;
        _projectService = projectService;
    }

    [HttpGet("/")]
    [HttpGet("dashboard/{projectId?}")]
    public async Task<IActionResult> Index(int? projectId, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login", "Auth");
        }

        var projects = await _projectService.GetProjectsForUserAsync(CurrentUserId.Value, includeArchived: false, ct);
        if (projects.Count == 0)
        {
            return RedirectToAction("Index", "Projects");
        }

        var selectedProjectId = projectId ?? projects[0].Id;
        var snapshot = await _dashboardService.GetSnapshotAsync(selectedProjectId, CurrentUserId.Value, ct);

        var vm = new DashboardPageViewModel
        {
            Shell = await BuildShellAsync("Dashboard", selectedProjectId, ct),
            Snapshot = snapshot
        };

        return View(vm);
    }
}
