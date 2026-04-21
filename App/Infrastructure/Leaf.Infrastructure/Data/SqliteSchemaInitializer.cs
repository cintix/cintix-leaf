using Leaf.Core.Security;
using Leaf.Core.Time;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Leaf.Infrastructure.Data;

public sealed class SqliteSchemaInitializer(
    ISqliteConnectionFactory connectionFactory,
    IPasswordHasher hasher,
    IClock clock,
    ILogger<SqliteSchemaInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        await ExecuteSchemaAsync(connection, transaction, ct);
        await SeedUsersAsync(connection, transaction, ct);
        await SeedProjectDataAsync(connection, transaction, ct);

        await transaction.CommitAsync(ct);
        logger.LogInformation("Leaf SQLite schema initialized");
    }

    private static async Task ExecuteSchemaAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS Users (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               DisplayName TEXT NOT NULL,
                               Email TEXT NOT NULL UNIQUE,
                               PasswordHash TEXT NOT NULL,
                               IsActive INTEGER NOT NULL,
                               IsAdmin INTEGER NOT NULL,
                               CreatedUtc TEXT NOT NULL
                           );

                           CREATE TABLE IF NOT EXISTS Projects (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               Key TEXT NOT NULL UNIQUE,
                               Name TEXT NOT NULL,
                               Description TEXT NOT NULL,
                               IsArchived INTEGER NOT NULL,
                               CreatedUtc TEXT NOT NULL
                           );

                           CREATE TABLE IF NOT EXISTS ProjectMembers (
                               ProjectId INTEGER NOT NULL,
                               UserId INTEGER NOT NULL,
                               AddedUtc TEXT NOT NULL,
                               PRIMARY KEY (ProjectId, UserId),
                               FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
                               FOREIGN KEY (UserId) REFERENCES Users(Id)
                           );

                           CREATE TABLE IF NOT EXISTS Sprints (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               ProjectId INTEGER NOT NULL,
                               Name TEXT NOT NULL,
                               Goal TEXT NOT NULL,
                               CapacityNote TEXT NOT NULL,
                               StartDate TEXT NOT NULL,
                               EndDate TEXT NOT NULL,
                               State INTEGER NOT NULL,
                               PlannedStoryPoints INTEGER NOT NULL,
                               CompletedStoryPoints INTEGER NOT NULL,
                               CreatedUtc TEXT NOT NULL,
                               ClosedUtc TEXT,
                               FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
                           );

                           CREATE TABLE IF NOT EXISTS WorkItems (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               ProjectId INTEGER NOT NULL,
                               SprintId INTEGER,
                               ParentId INTEGER,
                               Title TEXT NOT NULL,
                               Description TEXT NOT NULL,
                               Type INTEGER NOT NULL,
                               Status INTEGER NOT NULL,
                               Priority INTEGER NOT NULL,
                               AssigneeUserId INTEGER,
                               ReporterUserId INTEGER NOT NULL,
                               LabelsCsv TEXT NOT NULL,
                               StoryPoints INTEGER NOT NULL,
                               DueDate TEXT,
                               BacklogRank INTEGER NOT NULL,
                               ColumnOrder INTEGER NOT NULL,
                               CreatedUtc TEXT NOT NULL,
                               UpdatedUtc TEXT NOT NULL,
                               FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
                               FOREIGN KEY (SprintId) REFERENCES Sprints(Id) ON DELETE SET NULL,
                               FOREIGN KEY (ParentId) REFERENCES WorkItems(Id) ON DELETE SET NULL,
                               FOREIGN KEY (AssigneeUserId) REFERENCES Users(Id) ON DELETE SET NULL,
                               FOREIGN KEY (ReporterUserId) REFERENCES Users(Id)
                           );

                           CREATE TABLE IF NOT EXISTS Comments (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               WorkItemId INTEGER NOT NULL,
                               AuthorUserId INTEGER NOT NULL,
                               Body TEXT NOT NULL,
                               CreatedUtc TEXT NOT NULL,
                               UpdatedUtc TEXT,
                               FOREIGN KEY (WorkItemId) REFERENCES WorkItems(Id) ON DELETE CASCADE,
                               FOREIGN KEY (AuthorUserId) REFERENCES Users(Id)
                           );

                           CREATE TABLE IF NOT EXISTS Labels (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               ProjectId INTEGER NOT NULL,
                               Name TEXT NOT NULL,
                               ColorHex TEXT NOT NULL,
                               UNIQUE(ProjectId, Name),
                               FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
                           );

                           CREATE TABLE IF NOT EXISTS Activity (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               ProjectId INTEGER NOT NULL,
                               WorkItemId INTEGER,
                               ActorUserId INTEGER NOT NULL,
                               Kind TEXT NOT NULL,
                               Description TEXT NOT NULL,
                               CreatedUtc TEXT NOT NULL,
                               FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
                               FOREIGN KEY (WorkItemId) REFERENCES WorkItems(Id) ON DELETE SET NULL,
                               FOREIGN KEY (ActorUserId) REFERENCES Users(Id)
                           );

                           CREATE TABLE IF NOT EXISTS DashboardCache (
                               ProjectId INTEGER PRIMARY KEY,
                               CachedUtc TEXT NOT NULL,
                               PayloadJson TEXT NOT NULL,
                               FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
                           );

                           CREATE TABLE IF NOT EXISTS SprintDailyMetrics (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               SprintId INTEGER NOT NULL,
                               Day TEXT NOT NULL,
                               RemainingStoryPoints INTEGER NOT NULL,
                               UNIQUE(SprintId, Day),
                               FOREIGN KEY (SprintId) REFERENCES Sprints(Id) ON DELETE CASCADE
                           );

                           CREATE INDEX IF NOT EXISTS IX_Projects_Key ON Projects(Key);
                           CREATE INDEX IF NOT EXISTS IX_ProjectMembers_UserId ON ProjectMembers(UserId);
                           CREATE INDEX IF NOT EXISTS IX_Sprints_Project_State ON Sprints(ProjectId, State);
                           CREATE INDEX IF NOT EXISTS IX_WorkItems_Project_BacklogRank ON WorkItems(ProjectId, BacklogRank);
                           CREATE INDEX IF NOT EXISTS IX_WorkItems_Project_Sprint_Status ON WorkItems(ProjectId, SprintId, Status);
                           CREATE INDEX IF NOT EXISTS IX_WorkItems_Assignee ON WorkItems(AssigneeUserId);
                           CREATE INDEX IF NOT EXISTS IX_Comments_WorkItemId ON Comments(WorkItemId);
                           CREATE INDEX IF NOT EXISTS IX_Activity_Project_Created ON Activity(ProjectId, CreatedUtc DESC);
                           CREATE INDEX IF NOT EXISTS IX_Activity_WorkItemId ON Activity(WorkItemId);
                           """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task SeedUsersAsync(SqliteConnection connection, SqliteTransaction tx, CancellationToken ct)
    {
        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = tx;
        countCommand.CommandText = "SELECT COUNT(*) FROM Users;";
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct));

        if (count > 0)
        {
            return;
        }

        var now = clock.UtcNow.ToString("O");
        var adminHash = hasher.Hash("leafadmin");
        var emmaHash = hasher.Hash("leafdemo");
        var nikoHash = hasher.Hash("leafdemo");

        await InsertUserAsync(connection, tx, "Admin", "admin@leaf.local", adminHash, true, true, now, ct);
        await InsertUserAsync(connection, tx, "Emma North", "emma@leaf.local", emmaHash, true, false, now, ct);
        await InsertUserAsync(connection, tx, "Niko Lund", "niko@leaf.local", nikoHash, true, false, now, ct);
    }

    private static async Task InsertUserAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        string displayName,
        string email,
        string passwordHash,
        bool isActive,
        bool isAdmin,
        string now,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
                              INSERT INTO Users(DisplayName, Email, PasswordHash, IsActive, IsAdmin, CreatedUtc)
                              VALUES(@displayName, @email, @hash, @active, @admin, @created);
                              """;
        command.Parameters.AddWithValue("@displayName", displayName);
        command.Parameters.AddWithValue("@email", email);
        command.Parameters.AddWithValue("@hash", passwordHash);
        command.Parameters.AddWithValue("@active", isActive ? 1 : 0);
        command.Parameters.AddWithValue("@admin", isAdmin ? 1 : 0);
        command.Parameters.AddWithValue("@created", now);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task SeedProjectDataAsync(SqliteConnection connection, SqliteTransaction tx, CancellationToken ct)
    {
        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = tx;
        countCommand.CommandText = "SELECT COUNT(*) FROM Projects;";
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct));
        if (count > 0)
        {
            return;
        }

        var now = clock.UtcNow;

        await using var projectCmd = connection.CreateCommand();
        projectCmd.Transaction = tx;
        projectCmd.CommandText = """
                                 INSERT INTO Projects(Key, Name, Description, IsArchived, CreatedUtc)
                                 VALUES('LEAF', 'Leaf Platform', 'Launch milestone and design system stabilization', 0, @created);
                                 SELECT last_insert_rowid();
                                 """;
        projectCmd.Parameters.AddWithValue("@created", now.ToString("O"));
        var projectId = Convert.ToInt32(await projectCmd.ExecuteScalarAsync(ct));

        foreach (var userId in new[] { 1, 2, 3 })
        {
            await using var memberCmd = connection.CreateCommand();
            memberCmd.Transaction = tx;
            memberCmd.CommandText = "INSERT INTO ProjectMembers(ProjectId, UserId, AddedUtc) VALUES(@projectId, @userId, @created);";
            memberCmd.Parameters.AddWithValue("@projectId", projectId);
            memberCmd.Parameters.AddWithValue("@userId", userId);
            memberCmd.Parameters.AddWithValue("@created", now.ToString("O"));
            await memberCmd.ExecuteNonQueryAsync(ct);
        }

        await using var sprintCmd = connection.CreateCommand();
        sprintCmd.Transaction = tx;
        sprintCmd.CommandText = """
                                INSERT INTO Sprints(ProjectId, Name, Goal, CapacityNote, StartDate, EndDate, State, PlannedStoryPoints, CompletedStoryPoints, CreatedUtc)
                                VALUES(@projectId, 'Sprint 14', 'Ship baseline for work item workflows', 'Team capacity: 38 points', @start, @end, 2, 0, 0, @created);
                                SELECT last_insert_rowid();
                                """;
        sprintCmd.Parameters.AddWithValue("@projectId", projectId);
        sprintCmd.Parameters.AddWithValue("@start", DateOnly.FromDateTime(now.AddDays(-4)).ToString("yyyy-MM-dd"));
        sprintCmd.Parameters.AddWithValue("@end", DateOnly.FromDateTime(now.AddDays(10)).ToString("yyyy-MM-dd"));
        sprintCmd.Parameters.AddWithValue("@created", now.ToString("O"));
        var sprintId = Convert.ToInt32(await sprintCmd.ExecuteScalarAsync(ct));

        await InsertWorkItemAsync(connection, tx, projectId, sprintId, "Build dashboard shell", "Create polished navigation and summary cards", 2, 4, 2, 2, 1, "frontend,ux", 8, 1, now, ct);
        await InsertWorkItemAsync(connection, tx, projectId, sprintId, "Backlog drag and rank", "Enable smooth ranking in backlog", 2, 2, 3, 3, 1, "backlog,core", 5, 2, now, ct);
        await InsertWorkItemAsync(connection, tx, projectId, null, "API filters", "Advanced text + assignee + label filters", 3, 1, 2, 3, 1, "api", 3, 3, now, ct);

        foreach (var (name, color) in new[]
                 {
                     ("backend", "#486D8A"),
                     ("frontend", "#6BAA75"),
                     ("ux", "#D38F50"),
                     ("critical", "#C35454")
                 })
        {
            await using var labelCmd = connection.CreateCommand();
            labelCmd.Transaction = tx;
            labelCmd.CommandText = "INSERT INTO Labels(ProjectId, Name, ColorHex) VALUES(@projectId, @name, @color);";
            labelCmd.Parameters.AddWithValue("@projectId", projectId);
            labelCmd.Parameters.AddWithValue("@name", name);
            labelCmd.Parameters.AddWithValue("@color", color);
            await labelCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task InsertWorkItemAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int projectId,
        int? sprintId,
        string title,
        string description,
        int type,
        int status,
        int priority,
        int? assignee,
        int reporter,
        string labels,
        int storyPoints,
        int rank,
        DateTime now,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          INSERT INTO WorkItems(ProjectId, SprintId, ParentId, Title, Description, Type, Status, Priority, AssigneeUserId, ReporterUserId,
                                                LabelsCsv, StoryPoints, DueDate, BacklogRank, ColumnOrder, CreatedUtc, UpdatedUtc)
                          VALUES(@projectId, @sprintId, NULL, @title, @description, @type, @status, @priority, @assignee, @reporter,
                                 @labels, @storyPoints, NULL, @rank, @columnOrder, @created, @updated);
                          """;
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@sprintId", sprintId.HasValue ? sprintId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@description", description);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@priority", priority);
        cmd.Parameters.AddWithValue("@assignee", assignee.HasValue ? assignee.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@reporter", reporter);
        cmd.Parameters.AddWithValue("@labels", labels);
        cmd.Parameters.AddWithValue("@storyPoints", storyPoints);
        cmd.Parameters.AddWithValue("@rank", rank);
        cmd.Parameters.AddWithValue("@columnOrder", Math.Max(0, rank - 1));
        cmd.Parameters.AddWithValue("@created", now.ToString("O"));
        cmd.Parameters.AddWithValue("@updated", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
