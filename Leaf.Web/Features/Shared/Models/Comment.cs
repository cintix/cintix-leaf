namespace Leaf.Web.Features.Shared.Models;

public sealed class Comment
{
    public int Id { get; set; }
    public int WorkItemId { get; set; }
    public int AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
