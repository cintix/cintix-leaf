namespace Leaf.Web.Infrastructure.Data;

public sealed class LeafDatabaseOptions
{
    public const string SectionName = "LeafDatabase";
    public string Path { get; set; } = "Runtime/leaf.db";
}
