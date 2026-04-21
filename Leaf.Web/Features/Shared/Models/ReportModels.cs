namespace Leaf.Web.Features.Shared.Models;

public sealed record BurndownPoint(DateOnly Day, int RemainingPoints);
public sealed record VelocityPoint(string SprintName, int Planned, int Completed);
public sealed record ThroughputSummary(int CompletedItems, int AverageStoryPoints, int ActiveDays);
