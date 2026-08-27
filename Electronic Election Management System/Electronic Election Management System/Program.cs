using Electronic_Election_Management_System.Configuration;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Data.DesignTime;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.Data.Repositories.implementations;
using Electronic_Election_Management_System.Hubs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using Electronic_Election_Management_System.Services.interfaces;
using Electronic_Election_Management_System.Services.implementations;
using Electronic_Election_Management_System.Setup;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Electronic_Election_Management_System.Constants;

using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application");
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // data/dbconfig.json is the single source of truth for whether the app is configured.
    var dbConfig = DbConfigStore.TryLoad();
    var isConfigured = dbConfig is not null;

    if (isConfigured)
    {
        Log.Information("data/dbconfig.json found. Starting in fully-configured mode " +
                        "(provider: {Provider}).", dbConfig!.Provider);
    }
    else
    {
        Log.Warning(
            "data/dbconfig.json not found or invalid. Starting in unconfigured mode. " +
            "Only the /api/setup/* endpoints are available.");
    }

    var jwtOptions = JwtOptions.LoadAndValidate(builder.Configuration);
    builder.Services.AddSingleton(jwtOptions);
    if (isConfigured)
    {
        var provider = dbConfig!.Provider;
        var connectionString = dbConfig.ConnectionString;

        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // Set on the connection string so every connection EF opens inherits it.
            var sqliteConnectionStringBuilder = new SqliteConnectionStringBuilder(connectionString)
            {
                DefaultTimeout = 5
            };
            var sqliteCs = sqliteConnectionStringBuilder.ToString();

            builder.Services.AddDbContext<SqliteAppDbContext>(options =>
                options.UseSqlite(sqliteCs));
            builder.Services.AddScoped<ElectionDbContext>(sp =>
                sp.GetRequiredService<SqliteAppDbContext>());
        }
        else if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddDbContext<PostgresAppDbContext>(options =>
                options.UseNpgsql(connectionString));
            builder.Services.AddScoped<ElectionDbContext>(sp =>
                sp.GetRequiredService<PostgresAppDbContext>());
        }
        else
        {
            throw new InvalidOperationException(
                $"data/dbconfig.json contains unknown provider '{provider}'. " +
                "Supported values: 'Sqlite', 'Postgres'.");
        }

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IElectionRepository, ElectionRepository>();
        builder.Services.AddScoped<IElectionInvitationRepository, ElectionInvitationRepository>();
        builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        builder.Services.AddScoped<IVoteRepository, VoteRepository>();
        builder.Services.AddScoped<ILabelRepository, LabelRepository>();
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<IElectionImageRepository, ElectionImageRepository>();

        builder.Services.AddSingleton<ITokenService, TokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IElectionService, ElectionService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<IVoteService, VoteService>();
        builder.Services.AddScoped<IResultsService, ResultsService>();
        builder.Services.AddScoped<IScoringSchemeService, ScoringSchemeService>();
        builder.Services.AddScoped<ILabelService, LabelService>();
        builder.Services.AddSingleton<ICnpService, CnpService>();
        builder.Services.AddScoped<IUserNotifier, SignalRUserNotifier>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddScoped<IImageService, ImageService>();
    }

    // Registered in both modes, so the auth middleware is always present.
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            // The browser SignalR client cannot set an Authorization header on the handshake,
            // so it sends the JWT as a query param. Trusted for hub requests only.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },

                // Checked per request so a role change invalidates existing tokens at once.
                OnTokenValidated = async context =>
                {
                    var userIdClaim = context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier);
                    var tokenStamp = context.Principal.FindFirstValue("securityStamp");

                    if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                    {
                        context.Fail("Invalid token.");
                        return;
                    }

                    var users = context.HttpContext.RequestServices.GetService<IUserRepository>();
                    if (users is null)
                    {
                        // In unconfigured mode IUserRepository is not registered.
                        return;
                    }

                    var user = await users.GetByIdAsync(userId);

                    if (user is null || user.SecurityStamp != tokenStamp)
                    {
                        var log = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        log.LogWarning("Revoked token rejected for UserId {UserId}", userId);
                        context.Fail("revoked");
                    }
                },

                OnChallenge = async context =>
                {
                    // Replaces the default empty 401 with a typed body, so the frontend can tell
                    // expiry apart from revocation.
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    string reason = context.AuthenticateFailure switch
                    {
                        SecurityTokenExpiredException => "expired",
                        Exception ex when ex.Message == "revoked" => "revoked",
                        _ => "invalid"
                    };

                    await context.Response.WriteAsJsonAsync(new { reason });
                }
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddSignalR();
    builder.Services.AddControllers();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    const string AngularDevCorsPolicy = "AngularDevCorsPolicy";
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(AngularDevCorsPolicy, policy =>
        {
            policy
                .WithOrigins("http://localhost:4200", "https://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // required for the SignalR negotiate handshake
        });
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Election System API", Version = "v1" });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter: Bearer {token}"
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    var app = builder.Build();

    if (isConfigured)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectionDbContext>();
        db.Database.Migrate();

        if (dbConfig!.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // Enable WAL mode — SQLite-specific; must not run against Postgres.
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            db.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");

            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "PRAGMA journal_mode;";
            var currentJournalMode = (string?)await checkCmd.ExecuteScalarAsync() ?? "unknown";
            app.Logger.LogInformation(
                "SQLite journal_mode confirmed at startup: {JournalMode}", currentJournalMode);
        }

        await SeedData.EnsureScoringSchemesAsync(db);
        // Test data seeding is now handled during setup (SetupController) if opted in.

        // A creator who abandons the election form leaves an unattached image behind. Sweeping at
        // startup keeps that bounded without pulling in a scheduler.
        var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();
        await imageService.DeleteUnclaimedDraftsAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
    app.UseExceptionHandler();
    app.UseCors(AngularDevCorsPolicy);

    if (!isConfigured)
    {
        const string AppUnconfiguredMessage =
            "The application has not been configured yet. Complete first-run setup at POST /api/setup/save.";

        // Short-circuits ahead of authentication and controller dispatch, so unregistered
        // dependencies and token lookups are never reached.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api") &&
                !context.Request.Path.StartsWithSegments("/api/setup"))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "APP_UNCONFIGURED",
                    error = AppUnconfiguredMessage
                });
                return;
            }

            await next(context);
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();

    if (isConfigured)
    {
        app.MapControllers();
        app.MapHub<ResultsHub>("/hubs/results");
        app.MapHub<NotificationsHub>("/hubs/notifications");
    }
    else
    {
        app.MapControllers();
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
