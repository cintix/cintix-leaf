namespace Leaf.Web.Features.WorkItems;

public sealed class WorkItemDetailDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string ProjectKey { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public int? SprintId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int? AssigneeUserId { get; set; }
    public string? AssigneeName { get; set; }
    public int ReporterUserId { get; set; }
    public string? ReporterName { get; set; }
    public string LabelsCsv { get; set; } = string.Empty;
    public int StoryPoints { get; set; }
    public string? DueDate { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public List<CommentDto> Comments { get; set; } = [];
    public List<ActivityDto> Activity { get; set; } = [];
    public List<LabelDto> Labels { get; set; } = [];
    public List<UserDto> Users { get; set; } = [];
    public List<AttachmentDto> Attachments { get; set; } = [];
}

public sealed class AttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int UploadedByUserId { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
}

public sealed class CommentDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
}

public sealed class ActivityDto
{
    public int Id { get; set; }
    public int ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
}

public sealed class LabelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
