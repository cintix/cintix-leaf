namespace Leaf.Features.Shared.Contracts;

public sealed record CreateProjectInput(string Key, string Name, string Description, IReadOnlyCollection<int> MemberUserIds);
public sealed record UpdateProjectInput(int Id, string Name, string Description, bool IsArchived);
