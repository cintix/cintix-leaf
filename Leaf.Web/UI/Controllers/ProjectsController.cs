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

        var project = await _repository.GetProjectByKeyAsync(key.ToUpperInvariant(), ct);
        return RedirectToAction(nameof(Workspace), new { key = project?.Key ?? key.ToUpperInvariant() });
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Workspace(string key, string? q, int? assigneeId, int? sprintId, string? label, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login", "Auth");
        }

        var project = await _repository.GetProjectByKeyAsync(key.ToUpperInvariant(), ct);
        if (project is null)
        {
            return NotFound();
        }

        var searchFilters = new WorkItemSearchFilters(
            project.Id,
            q,
            assigneeId,
            sprintId,
            null,
            null,
            string.IsNullOrWhiteSpace(label) ? null : [label]);

        var members = await _projectService.GetMembersAsync(project.Id, ct);
        var users = await _userService.GetUsersAsync(includeInactive: false, ct);
        var sprints = await _sprintService.GetSprintsAsync(project.Id, ct);
        var activeSprint = await _sprintService.GetActiveSprintAsync(project.Id, ct);
        var backlog = await _searchService.SearchAsync(searchFilters, ct);
        var board = await _workItemService.GetBoardItemsAsync(project.Id, activeSprint?.Id, ct);
        var labels = await _repository.GetLabelsAsync(project.Id, ct);
        var activity = await _repository.GetRecentActivityAsync(project.Id, 20, ct);

        var vm = new ProjectWorkspaceViewModel
        {
            Shell = await BuildShellAsync("Projects", project.Id, ct),
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

    [HttpGet("{key}/{itemKey}")]
    public async Task<IActionResult> Issue(string key, string itemKey, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login", "Auth");
        }

        var project = await _repository.GetProjectByKeyAsync(key.ToUpperInvariant(), ct);
        if (project is null) return NotFound();

        var workItem = await _repository.GetWorkItemByKeyAsync(itemKey.ToUpperInvariant(), ct);
        if (workItem is null || workItem.ProjectId != project.Id) return NotFound();

        var detail = await _workItemService.GetDetailAsync(workItem.Id, ct);

        return View(detail);
    }

    [HttpPost("{key}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(string key, bool archived, CancellationToken ct)
    {
        var project = await _repository.GetProjectByKeyAsync(key.ToUpperInvariant(), ct);
        if (project is null) return NotFound();

        await _projectService.UpdateProjectAsync(new UpdateProjectInput(project.Id, project.Name, project.Description, archived), ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{key}/members")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMembers(string key, List<int> memberUserIds, CancellationToken ct)
    {
        var project = await _repository.GetProjectByKeyAsync(key.ToUpperInvariant(), ct);
        if (project is null) return NotFound();

        await _projectService.SetMembersAsync(project.Id, memberUserIds.Distinct().ToList(), ct);
        await _repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = project.Id,
            ActorUserId = CurrentUserId ?? 1,
            Kind = "membership_changed",
            Description = "Project member list updated",
            CreatedUtc = DateTime.UtcNow
        }, ct);

        return RedirectToAction(nameof(Workspace), new { key = project.Key });
    }
}
