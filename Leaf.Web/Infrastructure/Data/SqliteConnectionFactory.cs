using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Leaf.Web.Infrastructure.Data;

public sealed class SqliteConnectionFactory(IOptions<LeafDatabaseOptions> options) : ISqliteConnectionFactory
{
    private readonly string _dbPath = ResolvePath(options.Value.Path);

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var directory = System.IO.Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={_dbPath};Cache=Shared");
        await connection.OpenAsync(ct);

        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        await pragma.ExecuteNonQueryAsync(ct);

        return connection;
    }

    private static string ResolvePath(string configuredPath)
    {
        if (System.IO.Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
    }
}
