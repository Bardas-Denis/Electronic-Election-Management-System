using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Plugins;
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
    IHostApplicationLifetime lifetime,
    IPluginHost plugins) : ControllerBase
{

    private const string AlreadyConfiguredMessage =
        "The application is already configured. Remove data/dbconfig.json manually to reconfigure.";
    private const string UnknownProviderFormat =
        "Unknown provider '{0}'. Installed providers: {1}.";
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

    // GET /api/setup/available-providers

    /// <summary>
    /// Returns which database providers should be offered as choices on the setup screen.
    /// Comes from the plugin folder: a database the application can actually reach is one whose
    /// provider assembly is installed. Deleting that assembly removes the option.
    /// </summary>
    /// <returns><c>{ "providers": ["Sqlite", "Postgres"] }</c></returns>
    [HttpGet("available-providers")]
    public IActionResult GetAvailableProviders()
    {
        var providers = plugins.GetAll<IDatabaseProvider>().Select(p => p.Key).ToArray();

        return Ok(new { providers });
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

        if (!TryGetProvider(request.Provider, out var provider, out var providerError))
            return BadRequest(new { success = false, error = providerError });

        if (!provider.TrySanitizeConnectionString(
                request.ConnectionString, out var sanitizedCs, out var validationError))
        {
            return BadRequest(new { success = false, error = validationError });
        }

        var error = await provider.TestConnectionAsync(sanitizedCs, logger);

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

        if (!TryGetProvider(request.Provider, out var provider, out var providerError))
            return BadRequest(new { error = providerError });

        if (!provider.TrySanitizeConnectionString(
                request.ConnectionString, out var sanitizedCs, out var validationError))
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
        var connectionError = await provider.TestConnectionAsync(sanitizedCs, logger);

        if (connectionError is not null)
            return UnprocessableEntity(new { error = connectionError });

        // Apply migrations so the schema is ready before the app restarts.
        var migrationError = await ApplyMigrationsAsync(provider, sanitizedCs);
        if (migrationError is not null)
            return UnprocessableEntity(new { error = migrationError });

        // Create the admin user directly in the newly migrated database.
        var adminError = await CreateAdminUserAsync(provider, sanitizedCs,
            request.AdminEmail.Trim().ToLowerInvariant(), request.AdminPassword);
        if (adminError is not null)
            return UnprocessableEntity(new { error = adminError });

        // Seed test data if requested
        if (request.SeedData)
        {
            var seedError = await SeedTestDataAsync(provider, sanitizedCs);
            if (seedError is not null)
                return UnprocessableEntity(new { error = seedError });
        }

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

    private bool TryGetProvider(string provider, out IDatabaseProvider databaseProvider, out string error)
    {
        if (plugins.TryGet(provider, out databaseProvider))
        {
            error = string.Empty;
            return true;
        }

        var installed = plugins.GetAll<IDatabaseProvider>().Select(p => p.Key).ToList();
        error = string.Format(UnknownProviderFormat, provider,
            installed.Count > 0 ? string.Join(", ", installed) : "none");
        return false;
    }

    /// <summary>
    /// A short-lived context on a database that is not the one this process was configured with.
    /// </summary>
    private static ElectionDbContext OpenContext(IDatabaseProvider provider, string connectionString)
    {
        var options = new DbContextOptionsBuilder<ElectionDbContext>();
        provider.Configure(options, connectionString);
        return new ElectionDbContext(options.Options);
    }

    /// <summary>
    /// Creates the administrator user in the freshly-migrated database.
    /// Returns <see langword="null"/> on success or a short error message on failure.
    /// </summary>
    private async Task<string?> CreateAdminUserAsync(
        IDatabaseProvider provider, string connectionString, string email, string password)
    {
        try
        {
            var ctx = OpenContext(provider, connectionString);

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
    /// Seeds the database with test users, labels, and elections.
    /// Returns <see langword="null"/> on success or a short error message on failure.
    /// </summary>
    private async Task<string?> SeedTestDataAsync(
        IDatabaseProvider provider, string connectionString)
    {
        try
        {
            var ctx = OpenContext(provider, connectionString);

            await using (ctx)
            {
                await SeedData.EnsureTestDataAsync(ctx);
            }

            return null; // success
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed test data during setup.");
            return "Failed to seed test data. Check the server logs for details.";
        }
    }

    /// <summary>
    /// Builds a temporary, disposable DbContext for the chosen provider and applies
    /// all pending EF Core migrations. Returns <see langword="null"/> on success or
    /// a short error message on failure.
    /// </summary>
    private async Task<string?> ApplyMigrationsAsync(
        IDatabaseProvider provider, string connectionString)
    {
        try
        {
            // PrepareConnectionString is what creates a SQLite file's directory; without it the
            // very first connection fails on a fresh install.
            await using var ctx = OpenContext(provider, provider.PrepareConnectionString(connectionString));
            await ctx.Database.MigrateAsync();

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
    string? AdminPassword = null,
    bool SeedData = false);
