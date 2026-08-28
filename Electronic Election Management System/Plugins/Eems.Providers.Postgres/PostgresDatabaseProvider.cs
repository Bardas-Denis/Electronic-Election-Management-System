using Electronic_Election_Management_System.Data;
using Npgsql;
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

    private const string PostgresUnreachableMessage =
        "Could not reach the PostgreSQL server. Verify that the host is reachable, the port is "
        + "open, and the connection string is valid.";
    private const string PostgresUnexpectedMessage =
        "An unexpected error occurred while testing the PostgreSQL connection. Check the server "
        + "logs for details.";

    public bool TrySanitizeConnectionString(
        string rawConnectionString,
        out string sanitized,
        out string? error)
    {
        sanitized = string.Empty;
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

            sanitized = cleanBuilder.ConnectionString;
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

    public async Task<string?> TestConnectionAsync(string connectionString, ILogger logger)
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
