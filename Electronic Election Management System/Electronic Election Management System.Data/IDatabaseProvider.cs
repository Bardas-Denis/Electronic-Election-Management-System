using Electronic_Election_Management_System.PluginContracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Electronic_Election_Management_System.Data;

/// <summary>
/// A database engine the application can run on, supplied by an external assembly.
/// </summary>
/// <remarks>
/// The provider owns everything that differs between engines: how a context is pointed at the
/// database, what a valid connection string looks like, which migrations apply, and any setup
/// only that engine needs. Nothing outside this interface may name a specific database.
/// </remarks>
public interface IDatabaseProvider : IPlugin
{
    /// <summary>
    /// Points a context at this database. Implementations must also set
    /// <c>MigrationsAssembly</c> to their own assembly, since that is where their migrations live.
    /// </summary>
    void Configure(DbContextOptionsBuilder options, string connectionString);

    /// <summary>
    /// Last chance to adjust the connection string before anything opens it - SQLite creates the
    /// directory its file lives in, Postgres hands it back untouched.
    /// </summary>
    string PrepareConnectionString(string connectionString);

    /// <summary>
    /// Runs once after migrations have been applied, for setup only this engine needs.
    /// </summary>
    Task OnDatabaseReadyAsync(ElectionDbContext db, ILogger logger);

    /// <summary>
    /// Validates a connection string typed into the setup form and returns a sanitised copy.
    /// </summary>
    /// <remarks>
    /// This is a trust boundary, not a convenience: the value arrives from an anonymous request.
    /// Implementations are expected to constrain it - keeping a SQLite path inside the data
    /// directory, whitelisting the parameters a server connection may carry.
    /// </remarks>
    bool TrySanitizeConnectionString(string rawConnectionString, out string sanitized, out string? error);

    /// <summary>
    /// Probes the connection without writing anything or touching the schema.
    /// </summary>
    /// <returns>Null on success, or a short reason fit to show a user. Never raw exception text.</returns>
    Task<string?> TestConnectionAsync(string connectionString, ILogger logger);
}
