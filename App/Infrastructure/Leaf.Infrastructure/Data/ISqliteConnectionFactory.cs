using Microsoft.Data.Sqlite;

namespace Leaf.Infrastructure.Data;

public interface ISqliteConnectionFactory
{
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default);
}
