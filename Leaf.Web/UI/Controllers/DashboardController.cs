using Leaf.Web.Features.Dashboard;
using Leaf.Web.Features.Projects;
using Leaf.Web.Features.Shared;
using Leaf.Web.UI.Models.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Leaf.Web.UI.Controllers;

public sealed class DashboardController : LeafControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IProjectService _projectService;

    public DashboardController(
        IDashboardService dashboardService,
        ILeafRepository repository,
        IProjectService projectService)
        : base(repository, projectService)
    {
        _dashboardService = dashboardService;
        _projectService = projectService;
    }

    [HttpGet("/")]
    [HttpGet("dashboard/{projectKey?}")]
    public async Task<IActionResult> Index(string? projectKey, CancellationToken ct)
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

        Leaf.Web.Features.Shared.Models.Project? selectedProject;
        if (!string.IsNullOrWhiteSpace(projectKey))
        {
            selectedProject = await Repository.GetProjectByKeyAsync(projectKey.ToUpperInvariant(), ct);
            if (selectedProject is null) selectedProject = projects[0];
        }
        else
        {
            selectedProject = projects[0];
        }

        var snapshot = await _dashboardService.GetSnapshotAsync(selectedProject.Id, CurrentUserId.Value, ct);

        var vm = new DashboardPageViewModel
        {
            Shell = await BuildShellAsync("Dashboard", selectedProject.Id, ct),
            Snapshot = snapshot
        };

        return View(vm);
    }
}
