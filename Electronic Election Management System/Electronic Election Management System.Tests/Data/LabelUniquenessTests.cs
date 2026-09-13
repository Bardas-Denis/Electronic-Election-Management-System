using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Tests.Data;

/// <summary>
/// The sibling-uniqueness rule, exercised against a real database rather than a mocked
/// repository: the guarantee is an index, and only the engine can tell us it holds.
/// </summary>
public class LabelUniquenessTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ElectionDbContext _db;

    public LabelUniquenessTests()
    {
        // An in-memory database lives exactly as long as its open connection.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _db = new ElectionDbContext(
            new DbContextOptionsBuilder<ElectionDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private Label Country(string name, string code)
        => new() { Name = name, Code = code, Category = LabelCategories.Country };

    private static Label Under(Label parent, string name)
        => new() { Name = name, ParentId = parent.Id, Category = LabelCategories.Subdivision };

    [Fact]
    public async Task TwoSubdivisionsWithOneNameUnderDifferentCountries_BothSurvive()
    {
        var romania = Country("Romania", "RO");
        var moldova = Country("Moldova", "MD");
        _db.Labels.AddRange(romania, moldova);
        await _db.SaveChangesAsync();

        _db.Labels.AddRange(Under(romania, "Cluj"), Under(moldova, "Cluj"));

        await _db.SaveChangesAsync();

        (await _db.Labels.CountAsync(l => l.Name == "Cluj")).Should().Be(2);
    }

    [Fact]
    public async Task TwoSubdivisionsWithOneNameUnderTheSameCountry_AreRejected()
    {
        var romania = Country("Romania", "RO");
        _db.Labels.Add(romania);
        await _db.SaveChangesAsync();

        _db.Labels.AddRange(Under(romania, "Cluj"), Under(romania, "Cluj"));

        await _db.Invoking(d => d.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    // A composite unique index does not cover this on its own: both Postgres and SQLite treat
    // NULLs as distinct, so (NULL, 'Romania') twice passes. Countries are the roots of every
    // region lookup, so they need a filtered index of their own.
    [Fact]
    public async Task TwoCountriesWithOneName_AreRejected()
    {
        _db.Labels.AddRange(Country("Romania", "RO"), Country("Romania", "XX"));

        await _db.Invoking(d => d.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    // Labels outside the tree are roots too, and they must stay free to be named anything.
    [Fact]
    public async Task ANonGeographicLabelMayShareItsNameWithACountry()
    {
        _db.Labels.Add(Country("Romania", "RO"));
        await _db.SaveChangesAsync();

        _db.Labels.Add(new Label { Name = "Romania", Category = "employer" });

        await _db.Invoking(d => d.SaveChangesAsync()).Should().NotThrowAsync();
    }
}
