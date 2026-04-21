namespace Leaf.Core.Time;

public interface IClock
{
    DateTime UtcNow { get; }
}
