namespace Leaf.Features.Shared.Models;

public sealed class Project
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime CreatedUtc { get; set; }
}
