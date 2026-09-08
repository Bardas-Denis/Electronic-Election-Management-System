using System.Text.Json;
using System.Text.Json.Serialization;
using Electronic_Election_Management_System.DTOs;
using FluentAssertions;

namespace Electronic_Election_Management_System.Tests.Geo;

/// <summary>
/// Guards the seed file itself rather than the seeder. The file is generated from Natural Earth
/// by a one-off transformation, so the invariants the database schema relies on — unique codes,
/// no name clash between siblings, names short enough for the column — are properties of the
/// data, and only a test over the data can keep them true when someone regenerates it.
/// </summary>
public class WorldSubdivisionsDataTests
{
    private static readonly IReadOnlyList<SeedCountry> Countries = Load();

    [Fact]
    public void EveryCountryAndSubdivisionHasACodeAndAName()
    {
        Countries.Should().NotBeEmpty();

        foreach (var country in Countries)
        {
            country.Code.Should().NotBeNullOrWhiteSpace();
            country.Name.Should().NotBeNullOrWhiteSpace();

            foreach (var sub in country.Subs)
            {
                sub.Code.Should().NotBeNullOrWhiteSpace($"{country.Code} has a subdivision without a code");
                sub.Name.Should().NotBeNullOrWhiteSpace($"{country.Code} has a subdivision without a name");
            }
        }
    }

    [Fact]
    public void CodesAreUniqueAcrossTheWholeFile()
    {
        // The unique index on Label.Code covers countries and subdivisions alike, so a country
        // code colliding with a subdivision code would fail the seed just as surely.
        var all = Countries.Select(c => c.Code!)
            .Concat(Countries.SelectMany(c => c.Subs).Select(s => s.Code!))
            .ToList();

        all.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void NoTwoCountriesShareAName()
    {
        // Countries are roots, and in SQL two NULL parents never compare equal — so the
        // (ParentId, Name) index does NOT catch a duplicate here. Only this test does.
        Countries.Select(c => c.Name!.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void NoTwoSubdivisionsOfTheSameCountryShareAName()
    {
        foreach (var country in Countries)
        {
            country.Subs.Select(s => s.Name!.ToLowerInvariant())
                .Should().OnlyHaveUniqueItems($"{country.Code} would violate the unique index on (ParentId, Name)");
        }
    }

    [Fact]
    public void EveryNameFitsTheLabelNameColumn()
    {
        var names = Countries.Select(c => c.Name!)
            .Concat(Countries.SelectMany(c => c.Subs).Select(s => s.Name!));

        names.Should().OnlyContain(n => n.Length <= ValidationRules.LabelNameMaxLength);
    }

    [Fact]
    public void RomaniaCarriesItsFortyOneCountiesPlusBucharest()
    {
        // A canary: existing votes hold county names produced by CnpService, and they only match
        // the tree if Romania is seeded at county level rather than at some finer division.
        var romania = Countries.SingleOrDefault(c => c.Code == "RO");

        romania.Should().NotBeNull();
        romania!.Subs.Should().HaveCount(42);
        romania.Subs.Select(s => s.Name).Should().Contain("Cluj");
    }

    private static IReadOnlyList<SeedCountry> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Geo", "world-subdivisions.json");
        File.Exists(path).Should().BeTrue($"the seed file must be copied next to the test assembly ({path})");

        using var stream = File.OpenRead(path);
        var countries = JsonSerializer.Deserialize<List<SeedCountry>>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return countries ?? new List<SeedCountry>();
    }

    private sealed class SeedCountry
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("subs")] public List<SeedSubdivision> Subs { get; set; } = new();
    }

    private sealed class SeedSubdivision
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}
