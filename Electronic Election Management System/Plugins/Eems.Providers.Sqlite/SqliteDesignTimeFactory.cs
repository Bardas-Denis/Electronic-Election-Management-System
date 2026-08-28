using Electronic_Election_Management_System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eems.Providers.Sqlite;

/// <summary>
/// Lets <c>dotnet ef</c> work against this project, where the SQLite migrations now live.
/// </summary>
/// <remarks>
/// The connection string is never opened - the tooling only needs a configured context to compare
/// the model against the snapshot - so a throwaway file name is enough.
/// </remarks>
public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<ElectionDbContext>
{
    public ElectionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ElectionDbContext>();
        new SqliteDatabaseProvider().Configure(options, "Data Source=design-time.db");
        return new ElectionDbContext(options.Options);
    }
}
