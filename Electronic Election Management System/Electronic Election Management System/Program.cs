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


// Determine which database provider to use. Defaults to "Sqlite" when the key is absent.
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";

if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    // SQLite connection string with busy_timeout=5000ms.
    // "Default Timeout" (seconds) is mapped by Microsoft.Data.Sqlite todo
// sqlite3_busy_timeout, applied automatically on every connection opened(not just at startup).
    var sqliteConnectionStringBuilder = new SqliteConnectionStringBuilder(
        builder.Configuration.GetConnectionString("DefaultConnection"))
    {
        DefaultTimeout = 5 // 5s = 5000ms busy_timeout
    };
    var sqliteConnectionString = sqliteConnectionStringBuilder.ToString();

    builder.Services.AddDbContext<SqliteAppDbContext>(options =>
        options.UseSqlite(sqliteConnectionString));
    builder.Services.AddScoped<ElectionDbContext>(sp =>
        sp.GetRequiredService<SqliteAppDbContext>());
}
else if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var pgConnectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Postgres is required when Database:Provider is set to 'Postgres'.");

    builder.Services.AddDbContext<PostgresAppDbContext>(options =>
        options.UseNpgsql(pgConnectionString));
    builder.Services.AddScoped<ElectionDbContext>(sp =>
        sp.GetRequiredService<PostgresAppDbContext>());
}
else
{
    throw new InvalidOperationException(
        $"Unknown Database:Provider value: '{dbProvider}'. Supported values: 'Sqlite', 'Postgres'.");
}

var jwtOptions = JwtOptions.LoadAndValidate(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IElectionRepository, ElectionRepository>();
builder.Services.AddScoped<IElectionInvitationRepository, ElectionInvitationRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IVoteRepository, VoteRepository>();
builder.Services.AddScoped<ILabelRepository, LabelRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IElectionService, ElectionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IVoteService, VoteService>();
builder.Services.AddScoped<IResultsService, ResultsService>();
builder.Services.AddScoped<ILabelService, LabelService>();
builder.Services.AddSingleton<ICnpService, CnpService>();
builder.Services.AddScoped<IUserNotifier, SignalRUserNotifier>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddSignalR();

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

        // SignalR's browser client can't attach an Authorization header to the websocket/SSE
        // handshake, so it sends the JWT as an "access_token" query param instead
        // (see accessTokenFactory in results.service.ts). Only trust that for hub requests.
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
            // Verify security stamp for every request,
            // direct database query to immediately invalidate JWT tokens after role change.
            OnTokenValidated = async context =>
            {
                var userIdClaim = context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier);
                var tokenStamp = context.Principal.FindFirstValue("securityStamp");

                if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                {
                    context.Fail("Invalid token.");
                    return;
                }

                var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var user = await users.GetByIdAsync(userId);

                if (user is null || user.SecurityStamp != tokenStamp)
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Revoked token rejected for UserId {UserId}", userId);
                    context.Fail("revoked");
                }
            },
            OnChallenge = async context =>
            {
                // Suppress the default empty 401 and replace it with a typed JSON body
                // so the frontend can distinguish natural expiry from stamp-mismatch revocation.
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ElectionDbContext>();
    db.Database.Migrate();

    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        // Enable WAL mode — SQLite-specific; must not run against Postgres.
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        db.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "PRAGMA journal_mode;";
            var currentJournalMode = (string?)await checkCmd.ExecuteScalarAsync() ?? "unknown";
            app.Logger.LogInformation("SQLite journal_mode confirmed at startup: {JournalMode}", currentJournalMode);
        }
    }

    await SeedData.EnsureAdminUserAsync(db);
    if (app.Environment.IsDevelopment())
    {
        await SeedData.EnsureTestDataAsync(db);
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseCors(AngularDevCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ResultsHub>("/hubs/results");
app.MapHub<NotificationsHub>("/hubs/notifications");
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
