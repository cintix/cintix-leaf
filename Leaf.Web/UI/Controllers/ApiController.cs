using Leaf.Web.Features.Projects;
using Leaf.Web.Features.Reports;
using Leaf.Web.Features.Search;
using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;
using Leaf.Web.Features.Sprints;
using Leaf.Web.Features.WorkItems;
using Leaf.Web.UI.Infrastructure;
using Leaf.Web.UI.Models.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Leaf.Web.Infrastructure.Data;

namespace Leaf.Web.UI.Controllers;

[ApiController]
[Route("api")]
public sealed class ApiController : LeafControllerBase
{
    private readonly IWorkItemService _workItemService;
    private readonly ISprintService _sprintService;
    private readonly IReportService _reportService;
    private readonly ISearchService _searchService;
    private readonly ILeafRepository _repository;

    public ApiController(
        IWorkItemService workItemService,
        ISprintService sprintService,
        IReportService reportService,
        ISearchService searchService,
        IProjectService projectService,
        ILeafRepository repository)
        : base(repository, projectService)
    {
        _workItemService = workItemService;
        _sprintService = sprintService;
        _reportService = reportService;
        _searchService = searchService;
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

    [HttpGet("workitems/{workItemId:int}")]
    public async Task<IActionResult> GetWorkItemDetail(int workItemId, CancellationToken ct)
    {
        var detail = await _workItemService.GetDetailAsync(workItemId, ct);
        if (detail is null)
        {
            return this.ToErrorJson("Work item not found.", StatusCodes.Status404NotFound);
        }

        return Ok(detail);
    }

    [HttpPut("workitems/{workItemId:int}")]
    public async Task<IActionResult> UpdateWorkItem(int workItemId, [FromBody] UpdateWorkItemRequest request, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return this.ToErrorJson("Unauthorized", StatusCodes.Status401Unauthorized);
        }

        var result = await _workItemService.UpdateAsync(new UpdateWorkItemInput(
            workItemId,
            request.Title,
            request.Description,
            request.Status,
            request.Priority,
            request.AssigneeUserId,
            request.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            request.StoryPoints,
            string.IsNullOrWhiteSpace(request.DueDate) ? null : DateOnly.Parse(request.DueDate)), CurrentUserId.Value, ct);

        if (result.IsFailure)
        {
            return this.ToErrorJson(result.Error.Message);
        }

        return Ok(new { success = true });
    }

    [HttpDelete("workitems/{workItemId:int}")]
    public async Task<IActionResult> DeleteWorkItem(int workItemId, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return this.ToErrorJson("Unauthorized", StatusCodes.Status401Unauthorized);
        }

        var result = await _workItemService.DeleteAsync(workItemId, CurrentUserId.Value, ct);
        if (result.IsFailure)
        {
            return this.ToErrorJson(result.Error.Message, StatusCodes.Status404NotFound);
        }

        return Ok(new { success = true });
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

    [HttpGet("search/{projectId:int}")]
    public async Task<IActionResult> Search(int projectId, [FromQuery] string? q, [FromQuery] int? assigneeId, [FromQuery] int? sprintId, [FromQuery] int? status, [FromQuery] int? type, [FromQuery] string? label, CancellationToken ct)
    {
        var filters = new WorkItemSearchFilters(
            projectId,
            q,
            assigneeId,
            sprintId,
            status.HasValue ? (WorkItemStatus)status.Value : null,
            type.HasValue ? (WorkItemType)type.Value : null,
            string.IsNullOrWhiteSpace(label) ? null : [label]);

        var results = await _searchService.SearchAsync(filters, ct);

        var users = await _repository.GetUsersAsync(includeInactive: false, ct);
        var userMap = users.ToDictionary(u => u.Id, u => u.DisplayName);

        return Ok(results.Select(item => new
        {
            item.Id,
            item.Key,
            item.Title,
            item.Description,
            Type = item.Type.ToString(),
            Status = item.Status.ToString(),
            Priority = item.Priority.ToString(),
            item.AssigneeUserId,
            AssigneeName = item.AssigneeUserId.HasValue && userMap.TryGetValue(item.AssigneeUserId.Value, out var name) ? name : null,
            item.ReporterUserId,
            ReporterName = userMap.TryGetValue(item.ReporterUserId, out var rname) ? rname : null,
            item.LabelsCsv,
            item.StoryPoints,
            DueDate = item.DueDate?.ToString("yyyy-MM-dd"),
            item.BacklogRank,
            item.ColumnOrder
        }));
    }

    [HttpPost("workitems/{workItemId:int}/attachments")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadAttachment(int workItemId, IFormFile file, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return this.ToErrorJson("Unauthorized", StatusCodes.Status401Unauthorized);
        }

        if (file is null || file.Length == 0)
        {
            return this.ToErrorJson("No file provided.");
        }

        var item = await _workItemService.GetByIdAsync(workItemId, ct);
        if (item is null)
        {
            return this.ToErrorJson("Work item not found.", StatusCodes.Status404NotFound);
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);

        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var storedPath = Path.Combine(uploadsDir, storedName);

        await using var stream = new FileStream(storedPath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        var attachment = new Attachment
        {
            WorkItemId = workItemId,
            FileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = file.Length,
            StoredPath = storedName,
            UploadedByUserId = CurrentUserId.Value,
            CreatedUtc = DateTime.UtcNow
        };

        var id = await _repository.AddAttachmentAsync(attachment, ct);

        await _repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = item.ProjectId,
            WorkItemId = workItemId,
            ActorUserId = CurrentUserId.Value,
            Kind = "attachment_added",
            Description = $"Attached file: {file.FileName}",
            CreatedUtc = DateTime.UtcNow
        }, ct);

        return Ok(new { id, fileName = file.FileName });
    }

    [HttpGet("attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(int attachmentId, CancellationToken ct)
    {
        var attachment = await _repository.GetAttachmentAsync(attachmentId, ct);
        if (attachment is null)
        {
            return NotFound();
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        var filePath = Path.Combine(uploadsDir, attachment.StoredPath);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(stream, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("attachments/{attachmentId:int}")]
    public async Task<IActionResult> DeleteAttachment(int attachmentId, CancellationToken ct)
    {
        if (!CurrentUserId.HasValue)
        {
            return this.ToErrorJson("Unauthorized", StatusCodes.Status401Unauthorized);
        }

        var attachment = await _repository.GetAttachmentAsync(attachmentId, ct);
        if (attachment is null)
        {
            return this.ToErrorJson("Attachment not found.", StatusCodes.Status404NotFound);
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        var filePath = Path.Combine(uploadsDir, attachment.StoredPath);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        await _repository.DeleteAttachmentAsync(attachmentId, ct);
        return Ok(new { success = true });
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
