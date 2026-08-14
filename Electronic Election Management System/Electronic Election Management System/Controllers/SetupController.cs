using Electronic_Election_Management_System.Data.DesignTime;
using Electronic_Election_Management_System.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Controllers;

/// <summary>
/// First-run configuration endpoints. These endpoints are registered in both configured and
/// unconfigured mode, but <c>/api/setup/save</c> rejects requests when already configured.
/// </summary>
[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public sealed class SetupController(
    ILogger<SetupController> logger,
    IHostApplicationLifetime lifetime) : ControllerBase
{
    private const string AlreadyConfiguredMessage = 
        "The application is already configured. Remove data/dbconfig.json manually to reconfigure.";
    private const string UnknownProviderFormat = 
        "Unknown provider '{0}'. Supported values: Sqlite, Postgres.";
    private const string MigrationFailedMessage = 
        "Database migration failed. The connection was reachable, but the schema could not be applied. Check the server logs for details.";
    private const string SetupSuccessMessage = 
        "Configuration saved. The server is restarting — please wait a moment and then refresh the application.";

    // GET /api/setup/status

    /// <summary>
    /// Returns whether the application has already been configured via the setup flow.
    /// </summary>
    /// <returns><c>{ "configured": true|false }</c></returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new { configured = DbConfigStore.Exists() });
    }

    // POST /api/setup/test-connection

    /// <summary>
    /// Probes a database connection without writing any data or running migrations.
    /// Safe to call even when the application is already configured.
    /// </summary>
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] SetupRequest request)
    {
        if (!IsValidProvider(request.Provider, out var providerError))
            return BadRequest(new { success = false, error = providerError });

        var error = await SetupConnectionTester.TestAsync(
            request.Provider, request.ConnectionString, logger);

        if (error is not null)
            return Ok(new { success = false, error });

        return Ok(new { success = true });
    }

    // POST /api/setup/save

    /// <summary>
    /// Validates the provided connection, writes <c>data/dbconfig.json</c>,
    /// runs pending migrations, and then stops the process so an external
    /// restart policy can bring the app up in fully-configured mode.
    /// Returns <c>409 Conflict</c> if the application is already configured.
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SetupRequest request)
    {
        // Reject if already configured to prevent silent overwrites
        if (DbConfigStore.Exists())
        {
            return Conflict(new { error = AlreadyConfiguredMessage });
        }

        if (!IsValidProvider(request.Provider, out var providerError))
            return BadRequest(new { error = providerError });

        // Re-validate via a live connection before committing anything to disk.
        var connectionError = await SetupConnectionTester.TestAsync(
            request.Provider, request.ConnectionString, logger);

        if (connectionError is not null)
            return UnprocessableEntity(new { error = connectionError });

        // Apply migrations so the schema is ready before the app restarts.
        var migrationError = await ApplyMigrationsAsync(request.Provider, request.ConnectionString);
        if (migrationError is not null)
            return UnprocessableEntity(new { error = migrationError });

        // Persist the configuration file
        DbConfigStore.Save(new DbConfig(request.Provider, request.ConnectionString));

        logger.LogInformation(
            "Setup complete. Provider={Provider}. Triggering graceful shutdown for restart.",
            request.Provider);

        // Signal the host to stop.
        lifetime.StopApplication();

        return Ok(new { message = SetupSuccessMessage });
    }

    // Helpers

    private static bool IsValidProvider(string provider, out string error)
    {
        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            error = string.Empty;
            return true;
        }

        error = string.Format(UnknownProviderFormat, provider);
        return false;
    }

    /// <summary>
    /// Builds a temporary, disposable DbContext for the chosen provider and applies
    /// all pending EF Core migrations. Returns <see langword="null"/> on success or
    /// a short error message on failure.
    /// </summary>
    private async Task<string?> ApplyMigrationsAsync(string provider, string connectionString)
    {
        try
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
                    .UseSqlite(connectionString)
                    .Options;
                await using var ctx = new SqliteAppDbContext(options);
                await ctx.Database.MigrateAsync();
            }
            else
            {
                var options = new DbContextOptionsBuilder<PostgresAppDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;
                await using var ctx = new PostgresAppDbContext(options);
                await ctx.Database.MigrateAsync();
            }

            return null; // success
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Migration failed during setup for provider {Provider}.", provider);

            return MigrationFailedMessage;
        }
    }
}

// Request DTO

/// <summary>Body for <c>POST /api/setup/test-connection</c> and <c>POST /api/setup/save</c>.</summary>
public sealed record SetupRequest(string Provider, string ConnectionString);
