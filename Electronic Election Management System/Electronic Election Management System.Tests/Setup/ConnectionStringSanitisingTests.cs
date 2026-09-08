using Eems.Providers.Postgres;
using Eems.Providers.Sqlite;
using Npgsql;
using Xunit;

namespace Electronic_Election_Management_System.Tests.Setup;

public class ConnectionStringSanitisingTests
{
    // The sanitisers moved out of the application and into the provider plugins, but they still
    // guard the same trust boundary: a connection string typed into the anonymous setup form.
    private static readonly SqliteDatabaseProvider Sqlite = new();
    private static readonly PostgresDatabaseProvider Postgres = new();

    [Theory]
    [InlineData("Data Source=data/election.db")]
    [InlineData("Data Source=data/subdir/custom.db")]
    public void TrySanitizeConnectionString_ValidSqlitePath_Succeeds(string inputCs)
    {
        var valid = Sqlite.TrySanitizeConnectionString(
            inputCs, out var sanitized, out var error);

        Assert.True(valid);
        Assert.Null(error);
        Assert.Contains("Data Source=", sanitized);
    }

    [Theory]
    [InlineData("Data Source=../outside.db")]
    [InlineData("Data Source=data/../../etc/passwd")]
    [InlineData("Data Source=C:\\Windows\\System32\\test.db")]
    [InlineData("Data Source=/etc/election.db")]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=file:memdb1?mode=memory&cache=shared")]
    [InlineData("")]
    public void TrySanitizeConnectionString_InvalidOrEscapingSqlitePath_Fails(string inputCs)
    {
        var valid = Sqlite.TrySanitizeConnectionString(
            inputCs, out var sanitized, out var error);

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Empty(sanitized);
    }

    [Fact]
    public void TrySanitizeConnectionString_ValidPostgres_ProducesCanonicalWhitelistedString()
    {
        var rawCs = "Host=localhost;Port=5432;Database=election_db;Username=postgres;Password=supersecret";

        var valid = Postgres.TrySanitizeConnectionString(
            rawCs, out var sanitized, out var error);

        Assert.True(valid);
        Assert.Null(error);

        var builder = new NpgsqlConnectionStringBuilder(sanitized);
        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("election_db", builder.Database);
        Assert.Equal("postgres", builder.Username);
        Assert.Equal("supersecret", builder.Password);
    }

    [Fact]
    public void TrySanitizeConnectionString_PostgresParameterSmuggling_StripsDangerousParameters()
    {
        var smuggledCs = "Host=db.internal;Port=5432;Database=elections;Username=pguser;Password=pass123;" +
                         "Trust Server Certificate=true;SSL Mode=Disable;SearchPath=malicious;Command Timeout=999";

        var valid = Postgres.TrySanitizeConnectionString(
            smuggledCs, out var sanitized, out var error);

        Assert.True(valid);
        Assert.Null(error);

        var builder = new NpgsqlConnectionStringBuilder(sanitized);
        Assert.Equal("db.internal", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("elections", builder.Database);
        Assert.Equal("pguser", builder.Username);
        Assert.Equal("pass123", builder.Password);
        Assert.Null(builder.SearchPath);
        Assert.DoesNotContain("Trust Server Certificate", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SearchPath", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SSL Mode", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Host=;Port=5432;Database=db;Username=user;Password=pwd", "host")]
    [InlineData("Host=localhost;Port=99999;Database=db;Username=user;Password=pwd", "port")]
    [InlineData("Host=localhost;Port=5432;Database=;Username=user;Password=pwd", "database")]
    [InlineData("Host=localhost;Port=5432;Database=db;Username=;Password=pwd", "username")]
    [InlineData("Host=localhost;Port=5432;Database=db;Username=user;Password=", "password")]
    [InlineData("", "required")]
    public void TrySanitizeConnectionString_MissingOrInvalidPostgresFields_Fails(string invalidCs, string expectedErrorSubstring)
    {
        var valid = Postgres.TrySanitizeConnectionString(
            invalidCs, out var sanitized, out var error);

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Empty(sanitized);
        Assert.Contains(expectedErrorSubstring, error, StringComparison.OrdinalIgnoreCase);
    }
}
