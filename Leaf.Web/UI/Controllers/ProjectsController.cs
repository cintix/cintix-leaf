using Leaf.Web.Features.Projects;
using Leaf.Web.Features.Search;
using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;
using Leaf.Web.Features.Sprints;
using Leaf.Web.Features.Users;
using Leaf.Web.Features.WorkItems;
using Leaf.Web.UI.Models.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Leaf.Web.UI.Controllers;

[Route("projects")]
public sealed class ProjectsController : LeafControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IUserService _userService;
    private readonly IWorkItemService _workItemService;
    private readonly ISprintService _sprintService;
    private readonly ISearchService _searchService;
    private readonly ILeafRepository _repository;

    public ProjectsController(
        IProjectService projectService,
        IUserService userService,
        IWorkItemService workItemService,
        ISprintService sprintService,
        ISearchService searchService,
        ILeafRepository repository)
        : base(repository, projectService)
    {
        _projectService = projectService;
        _userService = userService;
        _workItemService = workItemService;
        _sprintService = sprintService;
        _searchService = searchService;
        _repository = repository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login", "Auth");
        }

        var shell = await BuildShellAsync("Projects", ct: ct);
        return View(shell);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string key, string name, string description, List<int>? memberUserIds, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login", "Auth");
        }

        var members = (memberUserIds ?? []).Append(CurrentUserId.Value).Distinct().ToList();
        var result = await _projectService.CreateProjectAsync(new CreateProjectInput(key, name, description, members), ct);

        if (result.IsFailure)
        {
            TempData["Error"] = result.Error.Message;
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Workspace), new { id = result.Value });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Workspace(int id, string? q, int? assigneeId, int? sprintId, string? label, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login", "Auth");
        }

        var project = await _projectService.GetProjectAsync(id, ct);
        if (project is null)
        {
            return NotFound();
        }

        var searchFilters = new WorkItemSearchFilters(
            id,
            q,
            assigneeId,
            sprintId,
            null,
            null,
            string.IsNullOrWhiteSpace(label) ? null : [label]);

        var members = await _projectService.GetMembersAsync(id, ct);
        var users = await _userService.GetUsersAsync(includeInactive: false, ct);
        var sprints = await _sprintService.GetSprintsAsync(id, ct);
        var activeSprint = await _sprintService.GetActiveSprintAsync(id, ct);
        var backlog = await _searchService.SearchAsync(searchFilters, ct);
        var board = await _workItemService.GetBoardItemsAsync(id, activeSprint?.Id, ct);
        var labels = await _repository.GetLabelsAsync(id, ct);
        var activity = await _repository.GetRecentActivityAsync(id, 20, ct);

        var vm = new ProjectWorkspaceViewModel
        {
            Shell = await BuildShellAsync("Projects", id, ct),
            Project = project,
            Members = members,
            Users = users,
            Sprints = sprints,
            ActiveSprint = activeSprint,
            Backlog = backlog,
            BoardItems = board,
            Labels = labels,
            Activity = activity,
            Query = q ?? string.Empty,
            AssigneeFilter = assigneeId,
            SprintFilter = sprintId,
            LabelFilter = label ?? string.Empty
        };

        return View(vm);
    }

    [HttpPost("{id:int}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, bool archived, CancellationToken ct)
    {
        var project = await _projectService.GetProjectAsync(id, ct);
        if (project is null)
        {
            return NotFound();
        }

        await _projectService.UpdateProjectAsync(new UpdateProjectInput(project.Id, project.Name, project.Description, archived), ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/members")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMembers(int id, List<int> memberUserIds, CancellationToken ct)
    {
        await _projectService.SetMembersAsync(id, memberUserIds.Distinct().ToList(), ct);
        await _repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = id,
            ActorUserId = CurrentUserId ?? 1,
            Kind = "membership_changed",
            Description = "Project member list updated",
            CreatedUtc = DateTime.UtcNow
        }, ct);

        return RedirectToAction(nameof(Workspace), new { id });
    }
}
