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
        try
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();
            return null; // success
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "SQLite connection probe failed. ConnectionString length: {Len}",
                connectionString.Length);

            return SqliteFailedMessage;
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
