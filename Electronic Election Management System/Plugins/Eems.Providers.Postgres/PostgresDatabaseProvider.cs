using Electronic_Election_Management_System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eems.Providers.Postgres;

/// <summary>
/// Runs the application on a PostgreSQL server.
/// </summary>
public sealed class PostgresDatabaseProvider : IDatabaseProvider
{
    /// <summary>Matches the value already stored in data/dbconfig.json, so existing
    /// installations keep resolving after this became a plugin.</summary>
    public string Key => "Postgres";

    public string DisplayName => "PostgreSQL";

    public void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(
            typeof(PostgresDatabaseProvider).Assembly.GetName().Name));
    }

    /// <remarks>
    /// Nothing to prepare: the server owns the storage, so there is no local file or directory to
    /// create, and Npgsql applies its own defaults for anything the string leaves out.
    /// </remarks>
    public string PrepareConnectionString(string connectionString) => connectionString;

    /// <remarks>
    /// Nothing to do. SQLite needs its journal mode set per database file; Postgres is configured
    /// on the server, outside this application's reach.
    /// </remarks>
    public Task OnDatabaseReadyAsync(ElectionDbContext db, ILogger logger) => Task.CompletedTask;
}
