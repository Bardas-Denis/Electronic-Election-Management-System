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

    private const string SqliteFailedMessage =
        "Could not open the SQLite database. Check that the path is writable and the connection "
        + "string is valid.";

    public bool TrySanitizeConnectionString(
        string rawConnectionString,
        out string sanitized,
        out string? error)
    {
        sanitized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            error = "SQLite connection string is required.";
            return false;
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder(rawConnectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource))
            {
                error = "SQLite Data Source (database path) is required.";
                return false;
            }

            if (builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase) ||
                builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                error = "In-memory or URI SQLite databases are not supported for persistent setup.";
                return false;
            }

            var allowedDataDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data"));
            var targetFullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), builder.DataSource));

            var allowedPrefix = allowedDataDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;

            if (!targetFullPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) &&
                !targetFullPath.Equals(allowedDataDir, StringComparison.OrdinalIgnoreCase))
            {
                error = "SQLite database path must reside within the 'data' directory.";
                return false;
            }

            var cleanBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = builder.DataSource,
                Mode = SqliteOpenMode.ReadWriteCreate
            };

            sanitized = cleanBuilder.ConnectionString;
            error = null;
            return true;
        }
        catch (Exception)
        {
            error = "Invalid SQLite connection string format.";
            return false;
        }
    }

    public async Task<string?> TestConnectionAsync(string connectionString, ILogger logger)
    {
        string? createdFilePath = null;
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(builder.DataSource) &&
                !builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase) &&
                !builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = Path.GetFullPath(builder.DataSource);
                if (!File.Exists(fullPath))
                {
                    createdFilePath = fullPath;
                }

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();
            await conn.CloseAsync();
            return null; // success
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "SQLite connection probe failed. ConnectionString length: {Len}",
                connectionString.Length);

            return SqliteFailedMessage;
        }
        finally
        {
            if (createdFilePath is not null && File.Exists(createdFilePath))
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    File.Delete(createdFilePath);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogDebug(cleanupEx, "Failed to clean up probe file {Path}", createdFilePath);
                }
            }
        }
    }
}
