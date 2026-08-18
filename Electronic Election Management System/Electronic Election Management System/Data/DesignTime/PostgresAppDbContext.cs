using Electronic_Election_Management_System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Electronic_Election_Management_System.Data.DesignTime
{
    // Design-time context for Postgres — used only by dotnet ef tooling (migrations add/remove).
    // To apply migrations: dotnet ef database update --context PostgresAppDbContext
    public class PostgresAppDbContext : ElectionDbContext
    {
        public PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : base(options) { }
    }

    // Factory that wires up Postgres when the EF tools invoke design-time discovery.
    public class PostgresAppDbContextFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
    {
        public PostgresAppDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var connStr = config.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:Postgres is missing from appsettings.json.");

            var options = new DbContextOptionsBuilder<PostgresAppDbContext>()
                .UseNpgsql(connStr)
                .Options;
            return new PostgresAppDbContext(options);
        }
    }
}
