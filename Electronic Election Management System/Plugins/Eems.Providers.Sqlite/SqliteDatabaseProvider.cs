using Electronic_Election_Management_System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eems.Providers.Sqlite;

/// <summary>
/// Runs the application on a SQLite file.
/// </summary>
public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    /// <summary>Matches the value already stored in data/dbconfig.json, so existing
    /// installations keep resolving after this became a plugin.</summary>
    public string Key => "Sqlite";

    public string DisplayName => "SQLite";

    public void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(
            typeof(SqliteDatabaseProvider).Assembly.GetName().Name));
    }

    public string PrepareConnectionString(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            // Set on the connection string so every connection EF opens inherits it.
            DefaultTimeout = 5
        };

        // SQLite creates the database file but not the folder holding it, so a fresh checkout
        // pointing at data/election.db would fail on the very first connection.
        if (!string.IsNullOrWhiteSpace(builder.DataSource))
        {
            var directory = Path.GetDirectoryName(builder.DataSource);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        return builder.ToString();
    }

    /// <remarks>
    /// WAL lets readers run while a writer holds the database, which the live results dashboard
    /// depends on; without it a poll during a vote blocks. The mode is read back because it is
    /// silently ignored on filesystems that cannot support it.
    /// </remarks>
    public async Task OnDatabaseReadyAsync(ElectionDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var journalMode = (string?)await command.ExecuteScalarAsync() ?? "unknown";

        logger.LogInformation("SQLite journal_mode confirmed at startup: {JournalMode}", journalMode);
    }
}
