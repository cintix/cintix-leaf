namespace Leaf.Web.Core.Time;

public interface IClock
{
    DateTime UtcNow { get; }
}
