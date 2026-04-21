namespace Leaf.Web.Features.Shared.Models;

public sealed class Label
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#6BAA75";
}
