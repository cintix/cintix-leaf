using Leaf.Features.Authentication.Core;
using Leaf.Features.Projects.Core;
using Leaf.Features.Shared.Contracts;
using Leaf.Features.Shared.Core;
using Leaf.Features.Users.Core;
using Leaf.Web.Models.Users;
using Microsoft.AspNetCore.Mvc;

namespace Leaf.Web.Controllers;

[Route("users")]
public sealed class UsersController(
    UserService userService,
    AuthService authService,
    ProjectService projectService,
    ILeafRepository repository) : LeafControllerBase(repository, projectService)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? projectId, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login", "Auth");
        }

        var users = await userService.GetUsersAsync(includeInactive: true, ct);
        var workload = projectId.HasValue
            ? await userService.GetWorkloadSummaryAsync(projectId.Value, ct)
            : new Dictionary<int, int>();

        var vm = new UsersPageViewModel
        {
            Shell = await BuildShellAsync("Users", projectId, ct),
            Users = users,
            WorkloadByUserId = workload,
            ProjectId = projectId
        };

        return View(vm);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string displayName, string email, string password, bool isAdmin, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(new CreateUserInput(displayName, email, password, isAdmin), ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string displayName, string email, bool isActive, bool isAdmin, CancellationToken ct)
    {
        var result = await userService.UpdateUserAsync(new UpdateUserInput(id, displayName, email, isActive, isAdmin), ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
