using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Data.DesignTime;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using Electronic_Election_Management_System.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Controllers;

/// <summary>
/// First-run configuration endpoints. These endpoints are registered in both configured and
/// unconfigured mode, but <c>/api/setup/save</c> and <c>/api/setup/test-connection</c> reject requests when already configured.
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
    private const string AdminEmailRequiredMessage = "Admin email is required.";
    private const string AdminEmailInvalidMessage = "Admin email is not a valid email address.";
    private const string AdminPasswordRequiredMessage = "Admin password is required.";
    private const string AdminPasswordTooShortMessage = "Admin password must be at least 8 characters.";

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
    /// Returns <c>409 Conflict</c> if the application is already configured.
    /// </summary>
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] SetupRequest request)
    {
        // Reject if already configured
        if (DbConfigStore.Exists())
        {
            return Conflict(new { error = AlreadyConfiguredMessage });
        }

        if (!IsValidProvider(request.Provider, out var providerError))
            return BadRequest(new { success = false, error = providerError });

        if (!SetupConnectionTester.TrySanitizeConnectionString(
                request.Provider, request.ConnectionString, out var sanitizedCs, out var validationError))
        {
            return BadRequest(new { success = false, error = validationError });
        }

        var error = await SetupConnectionTester.TestAsync(
            request.Provider, sanitizedCs, logger);

        if (error is not null)
            return Ok(new { success = false, error });

        return Ok(new { success = true });
    }

    // POST /api/setup/save

    /// <summary>
    /// Validates the provided connection and admin credentials, writes <c>data/dbconfig.json</c>,
    /// runs pending migrations, creates the first admin and then stops the process so an external
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

        if (!SetupConnectionTester.TrySanitizeConnectionString(
                request.Provider, request.ConnectionString, out var sanitizedCs, out var validationError))
        {
            return BadRequest(new { error = validationError });
        }

        // Validate admin credentials
        if (string.IsNullOrWhiteSpace(request.AdminEmail))
            return BadRequest(new { error = AdminEmailRequiredMessage });

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                request.AdminEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return BadRequest(new { error = AdminEmailInvalidMessage });

        if (string.IsNullOrWhiteSpace(request.AdminPassword))
            return BadRequest(new { error = AdminPasswordRequiredMessage });

        if (request.AdminPassword.Length < 8)
            return BadRequest(new { error = AdminPasswordTooShortMessage });

        // Re-validate via a live connection before committing anything to disk.
        var connectionError = await SetupConnectionTester.TestAsync(
            request.Provider, sanitizedCs, logger);

        if (connectionError is not null)
            return UnprocessableEntity(new { error = connectionError });

        // Apply migrations so the schema is ready before the app restarts.
        var migrationError = await ApplyMigrationsAsync(request.Provider, sanitizedCs);
        if (migrationError is not null)
            return UnprocessableEntity(new { error = migrationError });

        // Create the admin user directly in the newly migrated database.
        var adminError = await CreateAdminUserAsync(request.Provider, sanitizedCs,
            request.AdminEmail.Trim().ToLowerInvariant(), request.AdminPassword);
        if (adminError is not null)
            return UnprocessableEntity(new { error = adminError });

        // Persist the configuration file using the sanitized connection string
        DbConfigStore.Save(new DbConfig(request.Provider, sanitizedCs));

        logger.LogInformation(
            "Setup complete. Provider={Provider}. Admin={Email}. Triggering graceful shutdown for restart.",
            request.Provider, request.AdminEmail);

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
    /// Creates the administrator user in the freshly-migrated database.
    /// Returns <see langword="null"/> on success or a short error message on failure.
    /// </summary>
    private async Task<string?> CreateAdminUserAsync(
        string provider, string connectionString, string email, string password)
    {
        try
        {
            ElectionDbContext ctx = provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
                ? new SqliteAppDbContext(
                    new DbContextOptionsBuilder<SqliteAppDbContext>().UseSqlite(connectionString).Options)
                : new PostgresAppDbContext(
                    new DbContextOptionsBuilder<PostgresAppDbContext>().UseNpgsql(connectionString).Options);

            await using (ctx)
            {
                var admin = new User
                {
                    Email = email,
                    PasswordHash = PasswordHasher.Hash(password),
                    Role = UserRole.Admin
                };
                ctx.Users.Add(admin);
                await ctx.SaveChangesAsync();
            }

            return null; // success
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create admin user during setup.");
            return "Failed to create the administrator account. Check the server logs for details.";
        }
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
                var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
                if (!string.IsNullOrWhiteSpace(builder.DataSource))
                {
                    var dir = Path.GetDirectoryName(builder.DataSource);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }

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

/// <summary>
/// Body for <c>POST /api/setup/test-connection</c> and <c>POST /api/setup/save</c>.
/// <c>AdminEmail</c> and <c>AdminPassword</c> are only used by <c>POST /api/setup/save</c>.
/// </summary>
public sealed record SetupRequest(
    string Provider,
    string ConnectionString,
    string? AdminEmail = null,
    string? AdminPassword = null);
