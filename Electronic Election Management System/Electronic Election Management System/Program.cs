using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// SQLite connection string with busy_timeout=5000ms.
// "Default Timeout" (seconds) is mapped by Microsoft.Data.Sqlite todo
// sqlite3_busy_timeout, applied automatically on every connection opened(not just at startup).
var sqliteConnectionStringBuilder = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection"))
{
    DefaultTimeout = 5 // 5s = 5000ms busy_timeout
};
var sqliteConnectionString = sqliteConnectionStringBuilder.ToString();

builder.Services.AddDbContext<ElectionDbContext>(options =>
    options.UseSqlite(sqliteConnectionString)
);

builder.Services.AddSingleton<ITokenService, TokenService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IElectionRepository, ElectionRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IVoteRepository, VoteRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IElectionService, ElectionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IVoteService, VoteService>();
builder.Services.AddSingleton<ICnpService, CnpService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
string jwtKey = jwtSection["Key"]!;
string jwtIssuer = jwtSection["Issuer"]!;
string jwtAudience = jwtSection["Audience"]!;

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
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();

const string AngularDevCorsPolicy = "AngularDevCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
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

    // Enable WAL mode
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

    await SeedData.EnsureAdminUserAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors(AngularDevCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Seeds a default Admin account when the database is empty, so the team
// can log into the admin panel immediately without a manual setup step.
static class SeedData
{
    public static async Task EnsureAdminUserAsync(ElectionDbContext db)
    {
        bool anyUser = await db.Users.AnyAsync();
        if (anyUser)
        {
            return;
        }

        var admin = new User
        {
            Email = "admin@election.local",
            PasswordHash = PasswordHasher.Hash("Admin123!"),
            Role = UserRole.Admin
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
