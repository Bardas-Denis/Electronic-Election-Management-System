using System.Text;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- DbContext cu SQLite ---
builder.Services.AddDbContext<ElectionDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// --- Servicii aplicatie ---
builder.Services.AddSingleton<ITokenService, TokenService>();

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
