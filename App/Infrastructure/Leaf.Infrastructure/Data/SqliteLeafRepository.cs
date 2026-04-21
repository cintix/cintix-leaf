using System.Globalization;
using System.Text;
using Leaf.Features.Shared.Contracts;
using Leaf.Features.Shared.Core;
using Leaf.Features.Shared.Models;
using Microsoft.Data.Sqlite;

namespace Leaf.Infrastructure.Data;

public sealed class SqliteLeafRepository(ISqliteConnectionFactory connectionFactory) : ILeafRepository
{
    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, DisplayName, Email, PasswordHash, IsActive, IsAdmin, CreatedUtc FROM Users WHERE lower(Email) = lower(@email) LIMIT 1;";
        cmd.Parameters.AddWithValue("@email", email);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapUser(reader) : null;
    }

    public async Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, DisplayName, Email, PasswordHash, IsActive, IsAdmin, CreatedUtc FROM Users WHERE Id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapUser(reader) : null;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(bool includeInactive, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = includeInactive
            ? "SELECT Id, DisplayName, Email, PasswordHash, IsActive, IsAdmin, CreatedUtc FROM Users ORDER BY DisplayName;"
            : "SELECT Id, DisplayName, Email, PasswordHash, IsActive, IsAdmin, CreatedUtc FROM Users WHERE IsActive = 1 ORDER BY DisplayName;";

        var result = new List<User>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(MapUser(reader));
        }

        return result;
    }

    public async Task<int> CreateUserAsync(User user, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO Users(DisplayName, Email, PasswordHash, IsActive, IsAdmin, CreatedUtc)
                          VALUES(@displayName, @email, @passwordHash, @isActive, @isAdmin, @createdUtc);
                          SELECT last_insert_rowid();
                          """;
        cmd.Parameters.AddWithValue("@displayName", user.DisplayName);
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@isAdmin", user.IsAdmin ? 1 : 0);
        cmd.Parameters.AddWithValue("@createdUtc", user.CreatedUtc.ToString("O"));

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task UpdateUserAsync(UpdateUserInput input, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          UPDATE Users
                          SET DisplayName = @displayName,
                              Email = @email,
                              IsActive = @isActive,
                              IsAdmin = @isAdmin
                          WHERE Id = @id;
                          """;
        cmd.Parameters.AddWithValue("@id", input.Id);
        cmd.Parameters.AddWithValue("@displayName", input.DisplayName);
        cmd.Parameters.AddWithValue("@email", input.Email);
        cmd.Parameters.AddWithValue("@isActive", input.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@isAdmin", input.IsAdmin ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Project>> GetProjectsForUserAsync(int userId, bool includeArchived, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        if (includeArchived)
        {
            cmd.CommandText = """
                              SELECT p.Id, p.Key, p.Name, p.Description, p.IsArchived, p.CreatedUtc
                              FROM Projects p
                              INNER JOIN ProjectMembers pm ON pm.ProjectId = p.Id
                              WHERE pm.UserId = @userId
                              ORDER BY p.IsArchived ASC, p.Name;
                              """;
        }
        else
        {
            cmd.CommandText = """
                              SELECT p.Id, p.Key, p.Name, p.Description, p.IsArchived, p.CreatedUtc
                              FROM Projects p
                              INNER JOIN ProjectMembers pm ON pm.ProjectId = p.Id
                              WHERE pm.UserId = @userId
                                AND p.IsArchived = 0
                              ORDER BY p.Name;
                              """;
        }

        cmd.Parameters.AddWithValue("@userId", userId);

        var projects = new List<Project>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            projects.Add(MapProject(reader));
        }

        return projects;
    }

    public async Task<Project?> GetProjectAsync(int projectId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Key, Name, Description, IsArchived, CreatedUtc FROM Projects WHERE Id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", projectId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapProject(reader) : null;
    }

    public async Task<int> CreateProjectAsync(CreateProjectInput input, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow.ToString("O");
        int projectId;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                              INSERT INTO Projects(Key, Name, Description, IsArchived, CreatedUtc)
                              VALUES(@key, @name, @description, 0, @createdUtc);
                              SELECT last_insert_rowid();
                              """;
            cmd.Parameters.AddWithValue("@key", input.Key);
            cmd.Parameters.AddWithValue("@name", input.Name);
            cmd.Parameters.AddWithValue("@description", input.Description);
            cmd.Parameters.AddWithValue("@createdUtc", now);
            projectId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        var memberIds = input.MemberUserIds.Any() ? input.MemberUserIds : [1];
        foreach (var memberId in memberIds.Distinct())
        {
            await using var memberCmd = connection.CreateCommand();
            memberCmd.Transaction = tx;
            memberCmd.CommandText = "INSERT OR IGNORE INTO ProjectMembers(ProjectId, UserId, AddedUtc) VALUES(@projectId, @userId, @addedUtc);";
            memberCmd.Parameters.AddWithValue("@projectId", projectId);
            memberCmd.Parameters.AddWithValue("@userId", memberId);
            memberCmd.Parameters.AddWithValue("@addedUtc", now);
            await memberCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return projectId;
    }

    public async Task UpdateProjectAsync(UpdateProjectInput input, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          UPDATE Projects
                          SET Name = @name,
                              Description = @description,
                              IsArchived = @isArchived
                          WHERE Id = @id;
                          """;
        cmd.Parameters.AddWithValue("@id", input.Id);
        cmd.Parameters.AddWithValue("@name", input.Name);
        cmd.Parameters.AddWithValue("@description", input.Description);
        cmd.Parameters.AddWithValue("@isArchived", input.IsArchived ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<User>> GetProjectMembersAsync(int projectId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT u.Id, u.DisplayName, u.Email, u.PasswordHash, u.IsActive, u.IsAdmin, u.CreatedUtc
                          FROM Users u
                          INNER JOIN ProjectMembers pm ON pm.UserId = u.Id
                          WHERE pm.ProjectId = @projectId
                          ORDER BY u.DisplayName;
                          """;
        cmd.Parameters.AddWithValue("@projectId", projectId);

        var members = new List<User>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            members.Add(MapUser(reader));
        }

        return members;
    }

    public async Task ReplaceProjectMembersAsync(int projectId, IReadOnlyCollection<int> memberUserIds, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        await using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = "DELETE FROM ProjectMembers WHERE ProjectId = @projectId;";
            deleteCmd.Parameters.AddWithValue("@projectId", projectId);
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }

        var now = DateTime.UtcNow.ToString("O");
        foreach (var userId in memberUserIds.Distinct())
        {
            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText = "INSERT INTO ProjectMembers(ProjectId, UserId, AddedUtc) VALUES(@projectId, @userId, @addedUtc);";
            insertCmd.Parameters.AddWithValue("@projectId", projectId);
            insertCmd.Parameters.AddWithValue("@userId", userId);
            insertCmd.Parameters.AddWithValue("@addedUtc", now);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<WorkItem>> GetBacklogAsync(WorkItemSearchFilters filters, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);

        var sql = new StringBuilder(
            "SELECT Id, ProjectId, SprintId, ParentId, Title, Description, Type, Status, Priority, AssigneeUserId, ReporterUserId, LabelsCsv, StoryPoints, DueDate, BacklogRank, ColumnOrder, CreatedUtc, UpdatedUtc FROM WorkItems WHERE ProjectId = @projectId");

        var args = new List<(string Name, object Value)> { ("@projectId", filters.ProjectId) };

        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            sql.Append(" AND (Title LIKE @query OR Description LIKE @query)");
            args.Add(("@query", $"%{filters.Query.Trim()}%"));
        }

        if (filters.AssigneeUserId.HasValue)
        {
            sql.Append(" AND AssigneeUserId = @assignee");
            args.Add(("@assignee", filters.AssigneeUserId.Value));
        }

        if (filters.SprintId.HasValue)
        {
            sql.Append(" AND SprintId = @sprintId");
            args.Add(("@sprintId", filters.SprintId.Value));
        }

        if (filters.Status.HasValue)
        {
            sql.Append(" AND Status = @status");
            args.Add(("@status", (int)filters.Status.Value));
        }

        if (filters.Type.HasValue)
        {
            sql.Append(" AND Type = @type");
            args.Add(("@type", (int)filters.Type.Value));
        }

        if (filters.Labels is { Count: > 0 })
        {
            var i = 0;
            foreach (var label in filters.Labels.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var name = $"@label{i++}";
                sql.Append($" AND LabelsCsv LIKE {name}");
                args.Add((name, $"%{label.Trim()}%"));
            }
        }

        sql.Append(" ORDER BY BacklogRank, Id;");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql.ToString();
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        var items = new List<WorkItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapWorkItem(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<WorkItem>> GetBoardItemsAsync(int projectId, int? sprintId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        if (sprintId.HasValue)
        {
            cmd.CommandText = """
                              SELECT Id, ProjectId, SprintId, ParentId, Title, Description, Type, Status, Priority, AssigneeUserId, ReporterUserId, LabelsCsv, StoryPoints, DueDate, BacklogRank, ColumnOrder, CreatedUtc, UpdatedUtc
                              FROM WorkItems
                              WHERE ProjectId = @projectId
                                AND SprintId = @sprintId
                              ORDER BY Status, ColumnOrder, Id;
                              """;
            cmd.Parameters.AddWithValue("@sprintId", sprintId.Value);
        }
        else
        {
            cmd.CommandText = """
                              SELECT Id, ProjectId, SprintId, ParentId, Title, Description, Type, Status, Priority, AssigneeUserId, ReporterUserId, LabelsCsv, StoryPoints, DueDate, BacklogRank, ColumnOrder, CreatedUtc, UpdatedUtc
                              FROM WorkItems
                              WHERE ProjectId = @projectId
                              ORDER BY Status, ColumnOrder, Id;
                              """;
        }

        cmd.Parameters.AddWithValue("@projectId", projectId);

        var items = new List<WorkItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapWorkItem(reader));
        }

        return items;
    }

    public async Task<WorkItem?> GetWorkItemAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT Id, ProjectId, SprintId, ParentId, Title, Description, Type, Status, Priority, AssigneeUserId, ReporterUserId, LabelsCsv, StoryPoints, DueDate, BacklogRank, ColumnOrder, CreatedUtc, UpdatedUtc
                          FROM WorkItems
                          WHERE Id = @id
                          LIMIT 1;
                          """;
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapWorkItem(reader) : null;
    }

    public async Task<int> CreateWorkItemAsync(WorkItem item, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO WorkItems(ProjectId, SprintId, ParentId, Title, Description, Type, Status, Priority, AssigneeUserId, ReporterUserId,
                                                LabelsCsv, StoryPoints, DueDate, BacklogRank, ColumnOrder, CreatedUtc, UpdatedUtc)
                          VALUES(@projectId, @sprintId, @parentId, @title, @description, @type, @status, @priority, @assignee, @reporter,
                                 @labelsCsv, @storyPoints, @dueDate, @backlogRank, @columnOrder, @createdUtc, @updatedUtc);
                          SELECT last_insert_rowid();
                          """;
        cmd.Parameters.AddWithValue("@projectId", item.ProjectId);
        cmd.Parameters.AddWithValue("@sprintId", item.SprintId.HasValue ? item.SprintId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@parentId", item.ParentId.HasValue ? item.ParentId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@title", item.Title);
        cmd.Parameters.AddWithValue("@description", item.Description);
        cmd.Parameters.AddWithValue("@type", (int)item.Type);
        cmd.Parameters.AddWithValue("@status", (int)item.Status);
        cmd.Parameters.AddWithValue("@priority", (int)item.Priority);
        cmd.Parameters.AddWithValue("@assignee", item.AssigneeUserId.HasValue ? item.AssigneeUserId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@reporter", item.ReporterUserId);
        cmd.Parameters.AddWithValue("@labelsCsv", item.LabelsCsv);
        cmd.Parameters.AddWithValue("@storyPoints", item.StoryPoints);
        cmd.Parameters.AddWithValue("@dueDate", item.DueDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@backlogRank", item.BacklogRank);
        cmd.Parameters.AddWithValue("@columnOrder", item.ColumnOrder);
        cmd.Parameters.AddWithValue("@createdUtc", item.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedUtc", item.UpdatedUtc.ToString("O"));

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task UpdateWorkItemAsync(UpdateWorkItemInput input, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          UPDATE WorkItems
                          SET Title = @title,
                              Description = @description,
                              Status = @status,
                              Priority = @priority,
                              AssigneeUserId = @assignee,
                              LabelsCsv = @labels,
                              StoryPoints = @storyPoints,
                              DueDate = @dueDate,
                              UpdatedUtc = @updatedUtc
                          WHERE Id = @id;
                          """;
        cmd.Parameters.AddWithValue("@id", input.Id);
        cmd.Parameters.AddWithValue("@title", input.Title);
        cmd.Parameters.AddWithValue("@description", input.Description);
        cmd.Parameters.AddWithValue("@status", (int)input.Status);
        cmd.Parameters.AddWithValue("@priority", (int)input.Priority);
        cmd.Parameters.AddWithValue("@assignee", input.AssigneeUserId.HasValue ? input.AssigneeUserId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@labels", string.Join(',', input.Labels));
        cmd.Parameters.AddWithValue("@storyPoints", input.StoryPoints);
        cmd.Parameters.AddWithValue("@dueDate", input.DueDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MoveWorkItemAsync(int itemId, WorkItemStatus status, int columnOrder, int? sprintId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          UPDATE WorkItems
                          SET Status = @status,
                              ColumnOrder = @columnOrder,
                              SprintId = @sprintId,
                              UpdatedUtc = @updatedUtc
                          WHERE Id = @id;
                          """;
        cmd.Parameters.AddWithValue("@id", itemId);
        cmd.Parameters.AddWithValue("@status", (int)status);
        cmd.Parameters.AddWithValue("@columnOrder", Math.Max(0, columnOrder));
        cmd.Parameters.AddWithValue("@sprintId", sprintId.HasValue ? sprintId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ReorderBacklogAsync(int projectId, IReadOnlyList<int> orderedWorkItemIds, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        for (var i = 0; i < orderedWorkItemIds.Count; i++)
        {
            var id = orderedWorkItemIds[i];
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE WorkItems SET BacklogRank = @rank, UpdatedUtc = @updatedUtc WHERE Id = @id AND ProjectId = @projectId;";
            cmd.Parameters.AddWithValue("@rank", i + 1);
            cmd.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@projectId", projectId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task MoveWorkItemsToSprintAsync(int projectId, int sprintId, IReadOnlyCollection<int> itemIds, CancellationToken ct = default)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        foreach (var id in itemIds.Distinct())
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                              UPDATE WorkItems
                              SET SprintId = @sprintId,
                                  UpdatedUtc = @updatedUtc
                              WHERE Id = @id
                                AND ProjectId = @projectId;
                              """;
            cmd.Parameters.AddWithValue("@sprintId", sprintId);
            cmd.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@projectId", projectId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await UpdateSprintPlannedPointsAsync(connection, tx, sprintId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<Sprint>> GetSprintsAsync(int projectId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT Id, ProjectId, Name, Goal, CapacityNote, StartDate, EndDate, State, PlannedStoryPoints, CompletedStoryPoints, CreatedUtc, ClosedUtc
                          FROM Sprints
                          WHERE ProjectId = @projectId
                          ORDER BY State = 2 DESC, StartDate DESC;
                          """;
        cmd.Parameters.AddWithValue("@projectId", projectId);

        var result = new List<Sprint>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(MapSprint(reader));
        }

        return result;
    }

    public async Task<Sprint?> GetSprintAsync(int sprintId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT Id, ProjectId, Name, Goal, CapacityNote, StartDate, EndDate, State, PlannedStoryPoints, CompletedStoryPoints, CreatedUtc, ClosedUtc
                          FROM Sprints
                          WHERE Id = @id
                          LIMIT 1;
                          """;
        cmd.Parameters.AddWithValue("@id", sprintId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapSprint(reader) : null;
    }

    public async Task<Sprint?> GetActiveSprintAsync(int projectId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT Id, ProjectId, Name, Goal, CapacityNote, StartDate, EndDate, State, PlannedStoryPoints, CompletedStoryPoints, CreatedUtc, ClosedUtc
                          FROM Sprints
                          WHERE ProjectId = @projectId
                            AND State = 2
                          ORDER BY StartDate DESC
                          LIMIT 1;
                          """;
        cmd.Parameters.AddWithValue("@projectId", projectId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapSprint(reader) : null;
    }

    public async Task<int> CreateSprintAsync(Sprint sprint, IReadOnlyCollection<int> workItemIds, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var sprintId = 0;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                              INSERT INTO Sprints(ProjectId, Name, Goal, CapacityNote, StartDate, EndDate, State, PlannedStoryPoints, CompletedStoryPoints, CreatedUtc, ClosedUtc)
                              VALUES(@projectId, @name, @goal, @capacityNote, @startDate, @endDate, @state, 0, 0, @createdUtc, NULL);
                              SELECT last_insert_rowid();
                              """;
            cmd.Parameters.AddWithValue("@projectId", sprint.ProjectId);
            cmd.Parameters.AddWithValue("@name", sprint.Name);
            cmd.Parameters.AddWithValue("@goal", sprint.Goal);
            cmd.Parameters.AddWithValue("@capacityNote", sprint.CapacityNote);
            cmd.Parameters.AddWithValue("@startDate", sprint.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@endDate", sprint.EndDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@state", (int)sprint.State);
            cmd.Parameters.AddWithValue("@createdUtc", sprint.CreatedUtc.ToString("O"));
            sprintId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        if (workItemIds.Count > 0)
        {
            foreach (var id in workItemIds.Distinct())
            {
                await using var itemCmd = connection.CreateCommand();
                itemCmd.Transaction = tx;
                itemCmd.CommandText = """
                                      UPDATE WorkItems
                                      SET SprintId = @sprintId,
                                          UpdatedUtc = @updatedUtc
                                      WHERE Id = @id
                                        AND ProjectId = @projectId;
                                      """;
                itemCmd.Parameters.AddWithValue("@sprintId", sprintId);
                itemCmd.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
                itemCmd.Parameters.AddWithValue("@id", id);
                itemCmd.Parameters.AddWithValue("@projectId", sprint.ProjectId);
                await itemCmd.ExecuteNonQueryAsync(ct);
            }
        }

        await UpdateSprintPlannedPointsAsync(connection, tx, sprintId, ct);
        await tx.CommitAsync(ct);
        return sprintId;
    }

    public async Task StartSprintAsync(int sprintId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var projectId = 0;
        await using (var projectCmd = connection.CreateCommand())
        {
            projectCmd.Transaction = tx;
            projectCmd.CommandText = "SELECT ProjectId FROM Sprints WHERE Id = @id LIMIT 1;";
            projectCmd.Parameters.AddWithValue("@id", sprintId);
            projectId = Convert.ToInt32(await projectCmd.ExecuteScalarAsync(ct));
        }

        await using (var deactivateCmd = connection.CreateCommand())
        {
            deactivateCmd.Transaction = tx;
            deactivateCmd.CommandText = "UPDATE Sprints SET State = 1 WHERE ProjectId = @projectId AND State = 2 AND Id != @id;";
            deactivateCmd.Parameters.AddWithValue("@projectId", projectId);
            deactivateCmd.Parameters.AddWithValue("@id", sprintId);
            await deactivateCmd.ExecuteNonQueryAsync(ct);
        }

        await using (var activateCmd = connection.CreateCommand())
        {
            activateCmd.Transaction = tx;
            activateCmd.CommandText = "UPDATE Sprints SET State = 2 WHERE Id = @id;";
            activateCmd.Parameters.AddWithValue("@id", sprintId);
            await activateCmd.ExecuteNonQueryAsync(ct);
        }

        await RecordDailyRemainingAsync(connection, tx, sprintId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task CloseSprintAsync(int sprintId, bool moveUnfinishedToBacklog, int? nextSprintId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var completedPoints = 0;
        await using (var pointsCmd = connection.CreateCommand())
        {
            pointsCmd.Transaction = tx;
            pointsCmd.CommandText = "SELECT COALESCE(SUM(StoryPoints), 0) FROM WorkItems WHERE SprintId = @sprintId AND Status = 4;";
            pointsCmd.Parameters.AddWithValue("@sprintId", sprintId);
            completedPoints = Convert.ToInt32(await pointsCmd.ExecuteScalarAsync(ct));
        }

        if (moveUnfinishedToBacklog)
        {
            await using var moveCmd = connection.CreateCommand();
            moveCmd.Transaction = tx;
            moveCmd.CommandText = """
                                  UPDATE WorkItems
                                  SET SprintId = NULL,
                                      UpdatedUtc = @updatedUtc
                                  WHERE SprintId = @sprintId
                                    AND Status != 4;
                                  """;
            moveCmd.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
            moveCmd.Parameters.AddWithValue("@sprintId", sprintId);
            await moveCmd.ExecuteNonQueryAsync(ct);
        }
        else if (nextSprintId.HasValue)
        {
            await using var carryCmd = connection.CreateCommand();
            carryCmd.Transaction = tx;
            carryCmd.CommandText = """
                                   UPDATE WorkItems
                                   SET SprintId = @nextSprintId,
                                       UpdatedUtc = @updatedUtc
                                   WHERE SprintId = @sprintId
                                     AND Status != 4;
                                   """;
            carryCmd.Parameters.AddWithValue("@nextSprintId", nextSprintId.Value);
            carryCmd.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
            carryCmd.Parameters.AddWithValue("@sprintId", sprintId);
            await carryCmd.ExecuteNonQueryAsync(ct);

            await UpdateSprintPlannedPointsAsync(connection, tx, nextSprintId.Value, ct);
        }

        await using (var updateSprint = connection.CreateCommand())
        {
            updateSprint.Transaction = tx;
            updateSprint.CommandText = """
                                       UPDATE Sprints
                                       SET State = 3,
                                           CompletedStoryPoints = @completed,
                                           ClosedUtc = @closedUtc
                                       WHERE Id = @id;
                                       """;
            updateSprint.Parameters.AddWithValue("@completed", completedPoints);
            updateSprint.Parameters.AddWithValue("@closedUtc", DateTime.UtcNow.ToString("O"));
            updateSprint.Parameters.AddWithValue("@id", sprintId);
            await updateSprint.ExecuteNonQueryAsync(ct);
        }

        await RecordDailyRemainingAsync(connection, tx, sprintId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<int> AddCommentAsync(Comment comment, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO Comments(WorkItemId, AuthorUserId, Body, CreatedUtc, UpdatedUtc)
                          VALUES(@workItemId, @authorUserId, @body, @createdUtc, NULL);
                          SELECT last_insert_rowid();
                          """;
        cmd.Parameters.AddWithValue("@workItemId", comment.WorkItemId);
        cmd.Parameters.AddWithValue("@authorUserId", comment.AuthorUserId);
        cmd.Parameters.AddWithValue("@body", comment.Body);
        cmd.Parameters.AddWithValue("@createdUtc", comment.CreatedUtc.ToString("O"));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<Comment>> GetCommentsAsync(int workItemId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT Id, WorkItemId, AuthorUserId, Body, CreatedUtc, UpdatedUtc
                          FROM Comments
                          WHERE WorkItemId = @workItemId
                          ORDER BY CreatedUtc;
                          """;
        cmd.Parameters.AddWithValue("@workItemId", workItemId);

        var comments = new List<Comment>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            comments.Add(new Comment
            {
                Id = reader.GetInt32(0),
                WorkItemId = reader.GetInt32(1),
                AuthorUserId = reader.GetInt32(2),
                Body = reader.GetString(3),
                CreatedUtc = ParseDateTime(reader.GetString(4)),
                UpdatedUtc = reader.IsDBNull(5) ? null : ParseDateTime(reader.GetString(5))
            });
        }

        return comments;
    }

    public async Task<IReadOnlyList<Label>> GetLabelsAsync(int projectId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, ProjectId, Name, ColorHex FROM Labels WHERE ProjectId = @projectId ORDER BY Name;";
        cmd.Parameters.AddWithValue("@projectId", projectId);

        var labels = new List<Label>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            labels.Add(new Label
            {
                Id = reader.GetInt32(0),
                ProjectId = reader.GetInt32(1),
                Name = reader.GetString(2),
                ColorHex = reader.GetString(3)
            });
        }

        return labels;
    }

    public async Task<int> CreateLabelAsync(Label label, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO Labels(ProjectId, Name, ColorHex)
                          VALUES(@projectId, @name, @colorHex);
                          SELECT last_insert_rowid();
                          """;
        cmd.Parameters.AddWithValue("@projectId", label.ProjectId);
        cmd.Parameters.AddWithValue("@name", label.Name);
        cmd.Parameters.AddWithValue("@colorHex", label.ColorHex);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task AddActivityAsync(ActivityEntry activity, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO Activity(ProjectId, WorkItemId, ActorUserId, Kind, Description, CreatedUtc)
                          VALUES(@projectId, @workItemId, @actorUserId, @kind, @description, @createdUtc);
                          """;
        cmd.Parameters.AddWithValue("@projectId", activity.ProjectId);
        cmd.Parameters.AddWithValue("@workItemId", activity.WorkItemId.HasValue ? activity.WorkItemId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@actorUserId", activity.ActorUserId <= 0 ? 1 : activity.ActorUserId);
        cmd.Parameters.AddWithValue("@kind", activity.Kind);
        cmd.Parameters.AddWithValue("@description", activity.Description);
        cmd.Parameters.AddWithValue("@createdUtc", activity.CreatedUtc.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ActivityEntry>> GetRecentActivityAsync(int projectId, int take, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT Id, ProjectId, WorkItemId, ActorUserId, Kind, Description, CreatedUtc
                          FROM Activity
                          WHERE ProjectId = @projectId
                          ORDER BY CreatedUtc DESC
                          LIMIT @take;
                          """;
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@take", take);

        var result = new List<ActivityEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ActivityEntry
            {
                Id = reader.GetInt32(0),
                ProjectId = reader.GetInt32(1),
                WorkItemId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                ActorUserId = reader.GetInt32(3),
                Kind = reader.GetString(4),
                Description = reader.GetString(5),
                CreatedUtc = ParseDateTime(reader.GetString(6))
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<(DateOnly Day, int Remaining)>> GetBurndownAsync(int sprintId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Day, RemainingStoryPoints FROM SprintDailyMetrics WHERE SprintId = @sprintId ORDER BY Day;";
        cmd.Parameters.AddWithValue("@sprintId", sprintId);

        var result = new List<(DateOnly Day, int Remaining)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                result.Add((ParseDateOnly(reader.GetString(0)), reader.GetInt32(1)));
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        var sprint = await GetSprintAsync(sprintId, ct);
        if (sprint is null)
        {
            return [];
        }

        var days = Math.Max(1, sprint.EndDate.DayNumber - sprint.StartDate.DayNumber + 1);
        var planned = Math.Max(1, sprint.PlannedStoryPoints);

        for (var i = 0; i < days; i++)
        {
            var day = sprint.StartDate.AddDays(i);
            var remaining = Math.Max(0, planned - (planned * i / Math.Max(1, days - 1)));
            result.Add((day, remaining));
        }

        return result;
    }

    public async Task<IReadOnlyList<VelocityPoint>> GetVelocityAsync(int projectId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT Name, PlannedStoryPoints, CompletedStoryPoints
                          FROM Sprints
                          WHERE ProjectId = @projectId
                            AND State = 3
                          ORDER BY ClosedUtc DESC
                          LIMIT 8;
                          """;
        cmd.Parameters.AddWithValue("@projectId", projectId);

        var points = new List<VelocityPoint>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            points.Add(new VelocityPoint(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));
        }

        points.Reverse();
        return points;
    }

    public async Task<ThroughputSummary> GetThroughputSummaryAsync(int projectId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);

        var completedItems = 0;
        var avgStoryPoints = 0;
        var activeDays = 0;

        await using (var completedCmd = connection.CreateCommand())
        {
            completedCmd.CommandText = """
                                       SELECT COUNT(*), COALESCE(AVG(StoryPoints), 0)
                                       FROM WorkItems
                                       WHERE ProjectId = @projectId
                                         AND Status = 4;
                                       """;
            completedCmd.Parameters.AddWithValue("@projectId", projectId);

            await using var reader = await completedCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                completedItems = reader.GetInt32(0);
                avgStoryPoints = Convert.ToInt32(Math.Round(reader.GetDouble(1)));
            }
        }

        await using (var daysCmd = connection.CreateCommand())
        {
            daysCmd.CommandText = """
                                  SELECT COUNT(DISTINCT substr(UpdatedUtc, 1, 10))
                                  FROM WorkItems
                                  WHERE ProjectId = @projectId
                                    AND Status = 4;
                                  """;
            daysCmd.Parameters.AddWithValue("@projectId", projectId);
            activeDays = Convert.ToInt32(await daysCmd.ExecuteScalarAsync(ct));
        }

        return new ThroughputSummary(completedItems, avgStoryPoints, activeDays);
    }

    public Task SeedDemoDataIfEmptyAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    private static User MapUser(SqliteDataReader reader)
        => new()
        {
            Id = reader.GetInt32(0),
            DisplayName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            IsActive = reader.GetInt32(4) == 1,
            IsAdmin = reader.GetInt32(5) == 1,
            CreatedUtc = ParseDateTime(reader.GetString(6))
        };

    private static Project MapProject(SqliteDataReader reader)
        => new()
        {
            Id = reader.GetInt32(0),
            Key = reader.GetString(1),
            Name = reader.GetString(2),
            Description = reader.GetString(3),
            IsArchived = reader.GetInt32(4) == 1,
            CreatedUtc = ParseDateTime(reader.GetString(5))
        };

    private static WorkItem MapWorkItem(SqliteDataReader reader)
        => new()
        {
            Id = reader.GetInt32(0),
            ProjectId = reader.GetInt32(1),
            SprintId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            ParentId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            Title = reader.GetString(4),
            Description = reader.GetString(5),
            Type = (WorkItemType)reader.GetInt32(6),
            Status = (WorkItemStatus)reader.GetInt32(7),
            Priority = (WorkItemPriority)reader.GetInt32(8),
            AssigneeUserId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            ReporterUserId = reader.GetInt32(10),
            LabelsCsv = reader.GetString(11),
            StoryPoints = reader.GetInt32(12),
            DueDate = reader.IsDBNull(13) ? null : ParseDateOnly(reader.GetString(13)),
            BacklogRank = reader.GetInt32(14),
            ColumnOrder = reader.GetInt32(15),
            CreatedUtc = ParseDateTime(reader.GetString(16)),
            UpdatedUtc = ParseDateTime(reader.GetString(17))
        };

    private static Sprint MapSprint(SqliteDataReader reader)
        => new()
        {
            Id = reader.GetInt32(0),
            ProjectId = reader.GetInt32(1),
            Name = reader.GetString(2),
            Goal = reader.GetString(3),
            CapacityNote = reader.GetString(4),
            StartDate = ParseDateOnly(reader.GetString(5)),
            EndDate = ParseDateOnly(reader.GetString(6)),
            State = (SprintState)reader.GetInt32(7),
            PlannedStoryPoints = reader.GetInt32(8),
            CompletedStoryPoints = reader.GetInt32(9),
            CreatedUtc = ParseDateTime(reader.GetString(10)),
            ClosedUtc = reader.IsDBNull(11) ? null : ParseDateTime(reader.GetString(11))
        };

    private static DateOnly ParseDateOnly(string value)
        => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime ParseDateTime(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static async Task UpdateSprintPlannedPointsAsync(SqliteConnection connection, SqliteTransaction tx, int sprintId, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE Sprints
                          SET PlannedStoryPoints = (
                              SELECT COALESCE(SUM(StoryPoints), 0)
                              FROM WorkItems
                              WHERE SprintId = @sprintId
                          )
                          WHERE Id = @sprintId;
                          """;
        cmd.Parameters.AddWithValue("@sprintId", sprintId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task RecordDailyRemainingAsync(SqliteConnection connection, SqliteTransaction tx, int sprintId, CancellationToken ct)
    {
        var remaining = 0;

        await using (var remainCmd = connection.CreateCommand())
        {
            remainCmd.Transaction = tx;
            remainCmd.CommandText = """
                                    SELECT COALESCE(SUM(StoryPoints), 0)
                                    FROM WorkItems
                                    WHERE SprintId = @sprintId
                                      AND Status != 4;
                                    """;
            remainCmd.Parameters.AddWithValue("@sprintId", sprintId);
            remaining = Convert.ToInt32(await remainCmd.ExecuteScalarAsync(ct));
        }

        await using var upsertCmd = connection.CreateCommand();
        upsertCmd.Transaction = tx;
        upsertCmd.CommandText = """
                                INSERT INTO SprintDailyMetrics(SprintId, Day, RemainingStoryPoints)
                                VALUES(@sprintId, @day, @remaining)
                                ON CONFLICT(SprintId, Day)
                                DO UPDATE SET RemainingStoryPoints = excluded.RemainingStoryPoints;
                                """;
        upsertCmd.Parameters.AddWithValue("@sprintId", sprintId);
        upsertCmd.Parameters.AddWithValue("@day", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        upsertCmd.Parameters.AddWithValue("@remaining", remaining);
        await upsertCmd.ExecuteNonQueryAsync(ct);
    }
}
