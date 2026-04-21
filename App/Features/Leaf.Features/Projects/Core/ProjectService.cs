using Leaf.Core.Primitives;
using Leaf.Features.Shared.Contracts;
using Leaf.Features.Shared.Core;
using Leaf.Features.Shared.Models;

namespace Leaf.Features.Projects.Core;

public sealed class ProjectService(ILeafRepository repository)
{
    public Task<IReadOnlyList<Project>> GetProjectsForUserAsync(int userId, bool includeArchived = false, CancellationToken ct = default)
        => repository.GetProjectsForUserAsync(userId, includeArchived, ct);

    public Task<Project?> GetProjectAsync(int projectId, CancellationToken ct = default)
        => repository.GetProjectAsync(projectId, ct);

    public Task<IReadOnlyList<User>> GetMembersAsync(int projectId, CancellationToken ct = default)
        => repository.GetProjectMembersAsync(projectId, ct);

    public async Task<Result<int>> CreateProjectAsync(CreateProjectInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Key) || string.IsNullOrWhiteSpace(input.Name))
        {
            return Result<int>.Failure(Error.Validation("Project key and name are required."));
        }

        if (input.Key.Length is < 2 or > 8)
        {
            return Result<int>.Failure(Error.Validation("Project key must be 2-8 characters."));
        }

        var projectId = await repository.CreateProjectAsync(input with
        {
            Key = input.Key.Trim().ToUpperInvariant(),
            Name = input.Name.Trim(),
            Description = input.Description.Trim()
        }, ct);

        await repository.AddActivityAsync(new ActivityEntry
        {
            ProjectId = projectId,
            ActorUserId = input.MemberUserIds.FirstOrDefault(),
            Kind = "project_created",
            Description = "Project created",
            CreatedUtc = DateTime.UtcNow
        }, ct);

        return Result<int>.Success(projectId);
    }

    public async Task<Result> UpdateProjectAsync(UpdateProjectInput input, CancellationToken ct = default)
    {
        if (input.Id <= 0 || string.IsNullOrWhiteSpace(input.Name))
        {
            return Result.Failure(Error.Validation("Project id and name are required."));
        }

        await repository.UpdateProjectAsync(input with
        {
            Name = input.Name.Trim(),
            Description = input.Description.Trim()
        }, ct);

        return Result.Success();
    }

    public Task SetMembersAsync(int projectId, IReadOnlyCollection<int> memberUserIds, CancellationToken ct = default)
        => repository.ReplaceProjectMembersAsync(projectId, memberUserIds, ct);
}
