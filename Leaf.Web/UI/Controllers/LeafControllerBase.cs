using Leaf.Web.Features.Projects;
using Leaf.Web.Features.Shared;
using Leaf.Web.UI.Infrastructure;
using Leaf.Web.UI.Models.Shell;
using Microsoft.AspNetCore.Mvc;

namespace Leaf.Web.UI.Controllers;

public abstract class LeafControllerBase(
    ILeafRepository repository,
    IProjectService projectService) : Controller
{
    protected ILeafRepository Repository { get; } = repository;
    protected int? CurrentUserId => HttpContext.CurrentUserId();

    protected async Task<ShellContextViewModel> BuildShellAsync(string area, int? currentProjectId = null, CancellationToken ct = default)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue)
        {
            return new ShellContextViewModel { CurrentArea = area };
        }

        return new ShellContextViewModel
        {
            CurrentArea = area,
            CurrentProjectId = currentProjectId,
            CurrentUser = await Repository.GetUserByIdAsync(userId.Value, ct),
            Projects = await projectService.GetProjectsForUserAsync(userId.Value, includeArchived: false, ct)
        };
    }
}
