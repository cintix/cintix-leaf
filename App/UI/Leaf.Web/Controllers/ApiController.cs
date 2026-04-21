using Leaf.Features.Projects.Core;
using Leaf.Features.Reports.Core;
using Leaf.Features.Shared.Contracts;
using Leaf.Features.Shared.Core;
using Leaf.Features.Shared.Models;
using Leaf.Features.Sprints.Core;
using Leaf.Features.WorkItems.Core;
using Leaf.Web.Infrastructure;
using Leaf.Web.Models.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Leaf.Web.Controllers;

[ApiController]
[Route("api")]
public sealed class ApiController : LeafControllerBase
{
    private readonly WorkItemService _workItemService;
    private readonly SprintService _sprintService;
    private readonly ReportService _reportService;
    private readonly ILeafRepository _repository;

    public ApiController(
        WorkItemService workItemService,
        SprintService sprintService,
        ReportService reportService,
        ProjectService projectService,
        ILeafRepository repository)
        : base(repository, projectService)
    {
        _workItemService = workItemService;
        _sprintService = sprintService;
        _reportService = reportService;
        _repository = repository;
    }

    [HttpPost("workitems")]
    public async Task<IActionResult> CreateWorkItem([FromBody] CreateWorkItemRequest request, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return this.ToErrorJson("Unauthorized", StatusCodes.Status401Unauthorized);
        }

        var result = await _workItemService.CreateAsync(new CreateWorkItemInput(
            request.ProjectId,
            request.SprintId,
            null,
            request.Title,
            request.Description,
            request.Type,
            request.Priority,
            request.AssigneeUserId,
            CurrentUserId.Value,
            request.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            request.StoryPoints,
            null), ct);

        if (result.IsFailure)
        {
            return this.ToErrorJson(result.Error.Message);
        }

        return Ok(new { id = result.Value });
    }

    [HttpPost("workitems/reorder")]
    public async Task<IActionResult> ReorderBacklog([FromBody] ReorderBacklogRequest request, CancellationToken ct)
    {
        await _workItemService.ReorderBacklogAsync(request.ProjectId, request.OrderedIds, ct);
        return Ok(new { success = true });
    }

    [HttpPost("workitems/move")]
    public async Task<IActionResult> MoveWorkItem([FromBody] MoveWorkItemRequest request, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return this.ToErrorJson("Unauthorized", StatusCodes.Status401Unauthorized);
        }

        var result = await _workItemService.MoveAsync(request.WorkItemId, request.Status, request.ColumnOrder, request.SprintId, CurrentUserId.Value, ct);
        if (result.IsFailure)
        {
            return this.ToErrorJson(result.Error.Message, StatusCodes.Status404NotFound);
        }

        return Ok(new { success = true });
    }

    [HttpPost("workitems/comment")]
    public async Task<IActionResult> AddComment([FromBody] AddCommentRequest request, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return this.ToErrorJson("Unauthorized", StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return this.ToErrorJson("Comment body is required.");
        }

        var commentId = await _workItemService.AddCommentAsync(new Comment
        {
            WorkItemId = request.WorkItemId,
            AuthorUserId = CurrentUserId.Value,
            Body = request.Body.Trim(),
            CreatedUtc = DateTime.UtcNow
        }, ct);

        await _repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = (await _workItemService.GetByIdAsync(request.WorkItemId, ct))?.ProjectId ?? 0,
            WorkItemId = request.WorkItemId,
            ActorUserId = CurrentUserId.Value,
            Kind = "comment_added",
            Description = "Comment added",
            CreatedUtc = DateTime.UtcNow
        }, ct);

        return Ok(new { id = commentId });
    }

    [HttpPost("backlog/move-to-sprint")]
    public async Task<IActionResult> MoveBacklogToSprint([FromBody] MoveToSprintRequest request, CancellationToken ct)
    {
        await _workItemService.MoveToSprintAsync(request.ProjectId, request.SprintId, request.ItemIds, ct);
        return Ok(new { success = true });
    }

    [HttpPost("sprints")]
    public async Task<IActionResult> CreateSprint([FromBody] CreateSprintRequest request, CancellationToken ct)
    {
        var result = await _sprintService.CreateSprintAsync(new CreateSprintInput(
            request.ProjectId,
            request.Name,
            request.Goal,
            request.CapacityNote,
            request.StartDate,
            request.EndDate,
            request.WorkItemIds), ct);

        if (result.IsFailure)
        {
            return this.ToErrorJson(result.Error.Message);
        }

        return Ok(new { id = result.Value });
    }

    [HttpPost("sprints/{sprintId:int}/start")]
    public async Task<IActionResult> StartSprint(int sprintId, CancellationToken ct)
    {
        var result = await _sprintService.StartSprintAsync(sprintId, CurrentUserId ?? 1, ct);
        if (result.IsFailure)
        {
            return this.ToErrorJson(result.Error.Message, StatusCodes.Status404NotFound);
        }

        return Ok(new { success = true });
    }

    [HttpPost("sprints/{sprintId:int}/close")]
    public async Task<IActionResult> CloseSprint(int sprintId, [FromBody] CloseSprintRequest request, CancellationToken ct)
    {
        var result = await _sprintService.CloseSprintAsync(new CloseSprintInput(sprintId, request.MoveUnfinishedToBacklog, request.NextSprintId), CurrentUserId ?? 1, ct);
        if (result.IsFailure)
        {
            return this.ToErrorJson(result.Error.Message, StatusCodes.Status404NotFound);
        }

        return Ok(new { success = true });
    }

    [HttpPost("labels")]
    public async Task<IActionResult> CreateLabel([FromBody] CreateLabelRequest request, CancellationToken ct)
    {
        var id = await _repository.CreateLabelAsync(new Label
        {
            ProjectId = request.ProjectId,
            Name = request.Name.Trim(),
            ColorHex = request.ColorHex
        }, ct);

        return Ok(new { id });
    }

    [HttpGet("reports/{projectId:int}")]
    public async Task<IActionResult> Reports(int projectId, CancellationToken ct)
    {
        var velocity = await _reportService.GetVelocityAsync(projectId, ct);
        var throughput = await _reportService.GetThroughputSummaryAsync(projectId, ct);
        var active = await _sprintService.GetActiveSprintAsync(projectId, ct);
        var burndown = active is null ? [] : await _reportService.GetBurndownAsync(active.Id, ct);

        return Ok(new
        {
            velocity,
            throughput,
            burndown
        });
    }
}
