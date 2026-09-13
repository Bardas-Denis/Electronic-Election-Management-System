using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Electronic_Election_Management_System.Tests.Data;

/// <summary>
/// Assertions about the mapped EF model rather than about any one database. Both provider
/// plugins build their migrations from this single context, so a column missing here goes
/// missing on Postgres and SQLite alike.
/// </summary>
public class ElectionDbContextModelTests
{
    // Building the model never opens the connection, so the file named here is never created.
    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<ElectionDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var db = new ElectionDbContext(options);
        return db.Model;
    }

    private static IProperty? PropertyOf<TEntity>(string name)
        => Model.FindEntityType(typeof(TEntity))?.FindProperty(name);

    [Fact]
    public void UserDetails_CarriesResidenceCountry_AsANullableColumn()
    {
        var property = PropertyOf<UserDetails>("ResidenceCountry");

        property.Should().NotBeNull();
        // Nullable because the column lands on profiles that were saved before it existed.
        property!.IsNullable.Should().BeTrue();
        property.ClrType.Should().Be(typeof(string));
    }

    [Theory]
    [InlineData(typeof(UserDetails))]
    [InlineData(typeof(VoterDeclaration))]
    public void ResidenceCountyCode_IsMappedAndNullable(Type entity)
    {
        var property = Model.FindEntityType(entity)?.FindProperty("ResidenceCountyCode");

        property.Should().NotBeNull();
        // Plain text at the database level, like every other profile column; the length cap lives
        // on the DTO, which is where the request is rejected.
        property!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void VoterDeclaration_CarriesResidenceCountry_AsANullableColumn()
    {
        var property = PropertyOf<VoterDeclaration>("ResidenceCountry");

        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
        property.ClrType.Should().Be(typeof(string));
    }
}
