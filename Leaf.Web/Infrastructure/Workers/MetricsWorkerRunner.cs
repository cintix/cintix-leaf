using Leaf.Web.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Leaf.Web.Infrastructure.Workers;

public sealed class MetricsWorkerRunner(ISqliteConnectionFactory connectionFactory, ILogger<MetricsWorkerRunner> logger)
{
    public async Task RunTickAsync(CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct);

        var activeSprintIds = new List<int>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id FROM Sprints WHERE State = 2;";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                activeSprintIds.Add(reader.GetInt32(0));
            }
        }

        if (activeSprintIds.Count == 0)
        {
            return;
        }

        foreach (var sprintId in activeSprintIds)
        {
            var remaining = 0;
            await using (var sumCmd = connection.CreateCommand())
            {
                sumCmd.CommandText = "SELECT COALESCE(SUM(StoryPoints), 0) FROM WorkItems WHERE SprintId = @sprintId AND Status != 4;";
                sumCmd.Parameters.AddWithValue("@sprintId", sprintId);
                remaining = Convert.ToInt32(await sumCmd.ExecuteScalarAsync(ct));
            }

            await using var metricCmd = connection.CreateCommand();
            metricCmd.CommandText = """
                                    INSERT INTO SprintDailyMetrics(SprintId, Day, RemainingStoryPoints)
                                    VALUES(@sprintId, @day, @remaining)
                                    ON CONFLICT(SprintId, Day)
                                    DO UPDATE SET RemainingStoryPoints = excluded.RemainingStoryPoints;
                                    """;
            metricCmd.Parameters.AddWithValue("@sprintId", sprintId);
            metricCmd.Parameters.AddWithValue("@day", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
            metricCmd.Parameters.AddWithValue("@remaining", remaining);
            await metricCmd.ExecuteNonQueryAsync(ct);
        }

        logger.LogInformation("Metrics refresh completed for {Count} active sprints", activeSprintIds.Count);
    }
}
