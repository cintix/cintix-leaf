using Microsoft.Data.Sqlite;

namespace Leaf.Web.Infrastructure.Data;

public interface ISqliteConnectionFactory
{
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default);
}
