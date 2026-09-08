using Electronic_Election_Management_System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eems.Providers.Postgres;

/// <summary>
/// Lets <c>dotnet ef</c> work against this project, where the PostgreSQL migrations now live.
/// </summary>
/// <remarks>
/// Adding a migration never opens the connection, but removing one does: the tooling checks
/// whether it was already applied. The default matches the development container in
/// compose.yaml; override it with EEMS_DESIGNTIME_POSTGRES to point somewhere else.
/// </remarks>
public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<ElectionDbContext>
{
    public ElectionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ElectionDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("EEMS_DESIGNTIME_POSTGRES")
            ?? "Host=localhost;Port=5432;Database=eems;Username=eems_user;Password=changeme";

        new PostgresDatabaseProvider().Configure(options, connectionString);
        return new ElectionDbContext(options.Options);
    }
}
