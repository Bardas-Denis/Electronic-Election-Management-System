using System.Text;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Connection string SQLite cu busy_timeout=5000ms ---
// "Default Timeout" (secunde) e mapat de Microsoft.Data.Sqlite pe sqlite3_busy_timeout,
// aplicat automat pe FIECARE conexiune deschisa (nu doar la pornire).
var sqliteConnectionStringBuilder = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection"))
{
    DefaultTimeout = 5 // 5s = 5000ms busy_timeout
};
var sqliteConnectionString = sqliteConnectionStringBuilder.ToString();

// --- DbContext cu SQLite (WAL mode activat mai jos, dupa Migrate) ---
builder.Services.AddDbContext<ElectionDbContext>(options =>
    options.UseSqlite(sqliteConnectionString)
);

// --- Servicii aplicatie ---
builder.Services.AddSingleton<ITokenService, TokenService>();

// --- Repositories (Data Access layer) ---
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IElectionRepository, ElectionRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// --- Domain services (Business Logic layer) ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IElectionService, ElectionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// --- Autentificare JWT ---
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

// --- CORS pentru frontend-ul Angular (ng serve implicit pe 4200) ---
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

// --- Swagger cu suport pentru JWT (buton "Authorize") ---
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
        Description = "Introduceti: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// --- Creeaza / actualizeaza baza de date la pornire ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ElectionDbContext>();
    db.Database.Migrate();

    // --- Activare WAL mode (persista in fisierul .db, setat o singura data) ---
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
        app.Logger.LogInformation("SQLite journal_mode confirmat la pornire: {JournalMode}", currentJournalMode);
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

// ============================================================
// Seed: creeaza un cont Admin implicit daca baza de date e goala,
// ca echipa sa poata testa imediat panoul de administrare.
// ============================================================
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
