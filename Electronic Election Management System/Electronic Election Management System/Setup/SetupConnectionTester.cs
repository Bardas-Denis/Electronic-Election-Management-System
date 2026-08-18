using Microsoft.Data.Sqlite;
using Npgsql;

namespace Electronic_Election_Management_System.Setup;

/// <summary>
/// Probes a database connection without performing any writes or schema changes.
/// Used by both <c>POST /api/setup/test-connection</c> and
/// <c>POST /api/setup/save</c> before committing the configuration file.
/// </summary>
public static class SetupConnectionTester
{
    private const string UnknownProviderFormat = "Unknown provider '{0}'. Supported values: Sqlite, Postgres.";
    private const string SqliteFailedMessage = "Could not open the SQLite database. Check that the path is writable and the connection string is valid.";
    private const string PostgresUnreachableMessage = "Could not reach the PostgreSQL server. Verify that the host is reachable, the port is open, and the connection string is valid.";
    private const string PostgresUnexpectedMessage = "An unexpected error occurred while testing the PostgreSQL connection. Check the server logs for details.";

    /// <summary>
    /// Validates and sanitizes a connection string according to the provider rules.
    /// Ensures SQLite paths cannot escape the data directory and PostgreSQL parameters are strictly whitelisted.
    /// </summary>
    public static bool TrySanitizeConnectionString(
        string provider,
        string rawConnectionString,
        out string sanitizedConnectionString,
        out string? error)
    {
        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            return TrySanitizeSqliteConnectionString(rawConnectionString, out sanitizedConnectionString, out error);

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            return TrySanitizePostgresConnectionString(rawConnectionString, out sanitizedConnectionString, out error);

        sanitizedConnectionString = string.Empty;
        error = string.Format(UnknownProviderFormat, provider);
        return false;
    }

    private static bool TrySanitizeSqliteConnectionString(
        string rawConnectionString,
        out string sanitizedConnectionString,
        out string? error)
    {
        sanitizedConnectionString = string.Empty;
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

            sanitizedConnectionString = cleanBuilder.ConnectionString;
            error = null;
            return true;
        }
        catch (Exception)
        {
            error = "Invalid SQLite connection string format.";
            return false;
        }
    }

    private static bool TrySanitizePostgresConnectionString(
        string rawConnectionString,
        out string sanitizedConnectionString,
        out string? error)
    {
        sanitizedConnectionString = string.Empty;
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            error = "PostgreSQL connection string is required.";
            return false;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(rawConnectionString);

            if (string.IsNullOrWhiteSpace(builder.Host))
            {
                error = "PostgreSQL host is required.";
                return false;
            }

            if (builder.Port is < 1 or > 65535)
            {
                error = "PostgreSQL port must be between 1 and 65535.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(builder.Database))
            {
                error = "PostgreSQL database name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(builder.Username))
            {
                error = "PostgreSQL username is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(builder.Password))
            {
                error = "PostgreSQL password is required.";
                return false;
            }

            // Reconstruct canonical connection string containing ONLY whitelisted parameters
            // This strips any injected dangerous parameters (e.g. Options, SearchPath, TrustServerCertificate)
            var cleanBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = builder.Host,
                Port = builder.Port,
                Database = builder.Database,
                Username = builder.Username,
                Password = builder.Password,
                Timeout = 15,
                CommandTimeout = 30
            };

            sanitizedConnectionString = cleanBuilder.ConnectionString;
            error = null;
            return true;
        }
        catch (Exception)
        {
            error = "Invalid PostgreSQL connection string format.";
            return false;
        }
    }

    /// <summary>
    /// Attempts to open a live connection to the database described by
    /// <paramref name="provider"/> and <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="provider">
    /// Case-insensitive provider name — <c>"Sqlite"</c> or <c>"Postgres"</c>.
    /// </param>
    /// <param name="connectionString">Provider-specific connection string.</param>
    /// <param name="logger">Used to record the full exception on failure.</param>
    /// <returns>
    /// <see langword="null"/> when the connection succeeds;
    /// a short, human-readable failure reason when it does not.
    /// Raw exception details are never returned to the caller.
    /// </returns>
    public static async Task<string?> TestAsync(
        string provider,
        string connectionString,
        ILogger logger)
    {
        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            return await TestSqliteAsync(connectionString, logger);

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            return await TestPostgresAsync(connectionString, logger);

        return string.Format(UnknownProviderFormat, provider);
    }

    // Provider-specific probes

    private static async Task<string?> TestSqliteAsync(string connectionString, ILogger logger)
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

    private static async Task<string?> TestPostgresAsync(string connectionString, ILogger logger)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            return null; // success
        }
        catch (PostgresException ex)
        {
            // PostgresException is thrown when the *server* rejects the connection
            // (wrong password, database not found, etc.). ex.Message is the server's
            // own error message and does not contain the connection string or password.
            logger.LogError(ex,
                "PostgreSQL server rejected the connection probe. SqlState={SqlState}",
                ex.SqlState);

            return $"PostgreSQL error: {ex.Message}";
        }
        catch (NpgsqlException ex)
        {
            // Driver-level failure (host unreachable, timeout, TLS handshake failure, etc.)
            logger.LogError(ex,
                "PostgreSQL connection probe failed at the driver level. " +
                "ConnectionString length: {Len}", connectionString.Length);

            return PostgresUnreachableMessage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected exception during PostgreSQL connection probe. " +
                "ConnectionString length: {Len}", connectionString.Length);

            return PostgresUnexpectedMessage;
        }
    }
}
