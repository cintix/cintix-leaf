namespace Leaf.Web.Features.Shared.Models;

public sealed class Attachment
{
    public int Id { get; set; }
    public int WorkItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoredPath { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
}
