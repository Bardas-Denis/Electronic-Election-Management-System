using Electronic_Election_Management_System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Electronic_Election_Management_System.Data.DesignTime
{
    // Design-time context for SQLite — used only by dotnet ef tooling (migrations add/remove).
    // To apply migrations: dotnet ef database update --context SqliteAppDbContext
    public class SqliteAppDbContext : ElectionDbContext
    {
        public SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : base(options) { }
    }

    // Factory that wires up SQLite when the EF tools invoke design-time discovery.
    public class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
    {
        public SqliteAppDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var connStr = config.GetConnectionString("DefaultConnection")
                ?? "Data Source=election.db";

            var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
                .UseSqlite(connStr)
                .Options;
            return new SqliteAppDbContext(options);
        }
    }
}
